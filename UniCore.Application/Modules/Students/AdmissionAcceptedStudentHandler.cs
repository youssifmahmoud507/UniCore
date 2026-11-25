using System.Linq.Expressions;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Specifications;
using UniCore.Application.Modules.Audit;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Students
{
    // The Students side of an accepted admission: the applicant becomes a person profile and a student on the active plan of the
    // program, with a login account and the e-mail that tells them. Delivery is at-least-once, so it is idempotent: the audit
    // row ADMISSION_STUDENT_CREATED (entity = the application) says "done", and an earlier half-finished try is picked up again
    // (the person profile and the student are looked up before they are created).
    public sealed class AdmissionAcceptedStudentHandler(
        IRepository<AdmissionApplication> applications, IRepository<Applicant> applicants, IRepository<AcademicProgram> programs, IRepository<AcademicYear> years,
        IAggregateRepository<StudyPlan> plans, IAggregateRepository<StudyPlanVersion> versions, IAggregateRepository<PersonProfile> people, IAggregateRepository<Student> students,
        StudentAdmissionService admission, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit, IAuditLogReader reader)
    {
        public const string EntityName = "AdmissionApplication";

        private static readonly RequestInfo Origin = new(null, "admission-accepted");

        private readonly IRepository<AdmissionApplication> _applications = applications;
        private readonly IRepository<Applicant> _applicants = applicants;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IRepository<AcademicYear> _years = years;
        private readonly IAggregateRepository<StudyPlan> _plans = plans;
        private readonly IAggregateRepository<StudyPlanVersion> _versions = versions;
        private readonly IAggregateRepository<PersonProfile> _people = people;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly StudentAdmissionService _admission = admission;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;
        private readonly IAuditLogReader _reader = reader;

        // "Mona Ahmed Hassan" -> first "Mona", last "Ahmed Hassan"; a single name is used for both.
        public static (string First, string Last) SplitName(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return parts.Length switch
            {
                0 => (string.Empty, string.Empty),
                1 => (parts[0], parts[0]),
                _ => (parts[0], string.Join(' ', parts.Skip(1)))
            };
        }

        public async Task<Result> HandleAsync(AdmissionApplicationAccepted accepted, CancellationToken cancellationToken = default)
        {
            var entityId = accepted.ApplicationId.ToString();

            var done = await _reader.ExistsAsync(AuditActions.AdmissionStudentCreated, entityId, cancellationToken);
            if (!done.TryGetValue(out var alreadyDone))
            {
                return Result.Fail(done.Errors);
            }

            if (alreadyDone)
            {
                return Result.Ok();
            }

            var applicant = await _applicants.GetByIdAsync(accepted.ApplicantId, cancellationToken);
            var program = await _programs.GetByIdAsync(accepted.AcademicProgramId, cancellationToken);
            var year = await _years.GetByIdAsync(accepted.AcademicYearId, cancellationToken);

            if (applicant is null || program is null || year is null)
            {
                return await FailAsync(entityId, "ADMISSION_REFERENCE_NOT_FOUND", cancellationToken);
            }

            var today = _clock.UtcToday;
            var planVersion = await ActiveVersionOfProgramAsync(program.Id, today, cancellationToken);
            if (planVersion is null)
            {
                return await FailAsync(entityId, "ADMISSION_NO_ACTIVE_STUDY_PLAN", cancellationToken);
            }

            var person = await PersonOfAsync(applicant, today, cancellationToken);
            if (!person.TryGetValue(out var personId))
            {
                return await FailAsync(entityId, person.Error?.Code, person.Errors, cancellationToken);
            }

            // A student for this person from an earlier try: the work is done, only the audit row is missing.
            var existing = await _students.FirstOrDefaultAsync(new StudentByPersonSpecification(personId), cancellationToken);
            if (existing is not null)
            {
                await _audit.WriteAsync(new AuditEntry(AuditActions.AdmissionStudentCreated, null, EntityName, entityId, NewValues: $"student {existing.Id} (already existed)"), cancellationToken);

                return Result.Ok();
            }

            var command = new AdmitStudentCommand(personId, applicant.Email, today, today.AddYears(Math.Max(1, program.DurationYears)), program.Id, program.Code, planVersion.Id, year.Id, year.StartDate.Year, null, CreateAccount: true);

            var admitted = await _admission.AdmitAsync(command, Origin, cancellationToken);
            if (!admitted.TryGetValue(out var outcome))
            {
                return await FailAsync(entityId, admitted.Error?.Code, admitted.Errors, cancellationToken);
            }

            await _audit.WriteAsync(new AuditEntry(AuditActions.AdmissionStudentCreated, null, EntityName, entityId, NewValues: $"student {outcome.Student.Summary.Id}; account {(outcome.AccountProvisioned ? "created" : outcome.AccountErrorCode)}"), cancellationToken);

            return Result.Ok();
        }

        // Permanent problems are recorded for the registrar and returned (the outbox dead-letters them); the manual admission
        // (POST /students) stays available.
        private async Task<Result> FailAsync(string entityId, string? code, CancellationToken cancellationToken)
        {
            await _audit.WriteAsync(new AuditEntry(AuditActions.AdmissionStudentFailed, null, EntityName, entityId, NewValues: code), cancellationToken);

            return Result.Fail(Error.BusinessRule(code ?? "ADMISSION_STUDENT_FAILED", "The accepted applicant could not be made a student."));
        }

        private async Task<Result> FailAsync(string entityId, string? code, IReadOnlyList<Error> errors, CancellationToken cancellationToken)
        {
            await _audit.WriteAsync(new AuditEntry(AuditActions.AdmissionStudentFailed, null, EntityName, entityId, NewValues: code), cancellationToken);

            return Result.Fail(errors);
        }

        // The latest active version, in effect today, of an active plan of the program.
        private async Task<StudyPlanVersion?> ActiveVersionOfProgramAsync(Guid programId, DateOnly today, CancellationToken cancellationToken)
        {
            var planIds = (await _plans.ListAsync(new ActivePlansOfProgramSpecification(programId), cancellationToken)).Select(p => p.Id).ToArray();

            return planIds.Length == 0 ? null : await _versions.FirstOrDefaultAsync(new ActiveVersionOfPlansSpecification(planIds, today), cancellationToken);
        }

        // Reuses the profile of an earlier try (same name, birth date and e-mail) instead of creating a second one.
        private async Task<Result<Guid>> PersonOfAsync(Applicant applicant, DateOnly today, CancellationToken cancellationToken)
        {
            var (first, last) = SplitName(applicant.FullName);

            var found = await _people.FirstOrDefaultAsync(new PersonOfApplicantSpecification(first, last, applicant.DateOfBirth, applicant.Email), cancellationToken);
            if (found is not null)
            {
                return found.Id;
            }

            var created = PersonProfile.Create(first, last, applicant.DateOfBirth, null, null, applicant.Email, applicant.Phone, today, _clock.UtcNow);
            if (!created.TryGetValue(out var person))
            {
                return Result<Guid>.Fail(created.Errors);
            }

            _people.Add(person);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

            return saved.IsFailure ? Result<Guid>.Fail(saved.Errors) : person.Id;
        }
    }

    public sealed class ActivePlansOfProgramSpecification : Specification<StudyPlan>
    {
        public ActivePlansOfProgramSpecification(Guid programId)
        {
            SetCriteria(p => p.AcademicProgramId == programId && p.IsActive);
        }
    }

    public sealed class ActiveVersionOfPlansSpecification : Specification<StudyPlanVersion>
    {
        public ActiveVersionOfPlansSpecification(Guid[] planIds, DateOnly today)
        {
            Expression<Func<StudyPlanVersion, bool>> inEffect = v => planIds.Contains(v.StudyPlanId) && v.Status == StudyPlanVersionStatus.Active && v.EffectiveFrom <= today && (v.EffectiveTo == null || v.EffectiveTo >= today);

            SetCriteria(inEffect);
            ApplyOrderByDescending(v => v.EffectiveFrom);
        }
    }

    public sealed class PersonOfApplicantSpecification : Specification<PersonProfile>
    {
        public PersonOfApplicantSpecification(string first, string last, DateOnly dateOfBirth, string email)
        {
            SetCriteria(p => p.FirstName == first && p.LastName == last && p.DateOfBirth == dateOfBirth && p.Email == email);
        }
    }

    public sealed class AdmissionAcceptedHandler(AdmissionAcceptedStudentHandler handler) : IDomainEventHandler<AdmissionApplicationAccepted>
    {
        public Task<Result> HandleAsync(AdmissionApplicationAccepted domainEvent, CancellationToken cancellationToken = default) => handler.HandleAsync(domainEvent, cancellationToken);
    }
}
