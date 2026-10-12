using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    // The admission handoff. Developer 1 calls AdmitAsync when an admission is accepted (StudentAdmitted);
    // the API calls it after checking Students.Manage. This service does no permission check itself.
    public sealed class StudentAdmissionService(IRepository<Student> students, IAcademicPlacementVerifier verifier, IStudentNumberGenerator numbers, IStudentAccountProvisioner provisioner, IUnitOfWork unitOfWork, IClock clock, IEmailQueue emailQueue, IPasswordResetSettings settings, IAuditWriter audit, ICurrentUser user)
    {
        private const string ProbeNumber = "PROBE";

        private readonly IRepository<Student> _students = students;
        private readonly IAcademicPlacementVerifier _verifier = verifier;
        private readonly IStudentNumberGenerator _numbers = numbers;
        private readonly IStudentAccountProvisioner _provisioner = provisioner;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IEmailQueue _emailQueue = emailQueue;
        private readonly IPasswordResetSettings _settings = settings;
        private readonly IAuditWriter _audit = audit;
        private readonly ICurrentUser _user = user;

        public async Task<Result<AdmissionOutcome>> AdmitAsync(AdmitStudentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.CreateAccount && !EmailAddressValidator.IsValid(command.Email))
            {
                return IdentityErrors.UserEmailInvalid;
            }

            var now = _clock.UtcNow;

            // Dry run of the real validation, so no student number is spent on invalid input.
            var probe = Student.Create(
                command.PersonProfileId, ProbeNumber, command.AdmissionDate, command.ExpectedGraduationDate,
                command.AdmissionAcademicYearId, command.AcademicProgramId, command.StudyPlanVersionId,
                command.AcademicLevelId, now);

            if (probe.IsFailure)
            {
                return Result<AdmissionOutcome>.Fail(probe.Errors);
            }

            if (await _students.AnyAsync(new StudentByPersonSpecification(command.PersonProfileId), cancellationToken))
            {
                return StudentErrors.AlreadyExists;
            }

            var verified = await _verifier.VerifyAdmissionAsync(
                new AdmissionPlacement(
                    command.AcademicProgramId, command.ProgramCode, command.StudyPlanVersionId,
                    command.AdmissionAcademicYearId, command.AdmissionYear, command.AcademicLevelId),
                cancellationToken);

            if (verified.IsFailure)
            {
                return Result<AdmissionOutcome>.Fail(verified.Errors);
            }

            var number = await _numbers.NextAsync(command.AdmissionYear, command.ProgramCode, cancellationToken);
            if (!number.TryGetValue(out var studentNumber))
            {
                return Result<AdmissionOutcome>.Fail(number.Errors);
            }

            var created = Student.Create(
                command.PersonProfileId, studentNumber, command.AdmissionDate, command.ExpectedGraduationDate,
                command.AdmissionAcademicYearId, command.AcademicProgramId, command.StudyPlanVersionId,
                command.AcademicLevelId, now);

            if (!created.TryGetValue(out var student))
            {
                return Result<AdmissionOutcome>.Fail(created.Errors);
            }

            _students.Add(student);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<AdmissionOutcome>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.StudentCreated, _user.UserId, "Student", student.Id.ToString(),
                    request.IpAddress, request.UserAgent, NewValues: student.StudentNumber),
                cancellationToken);

            var accountProvisioned = false;
            string? accountError = null;

            if (command.CreateAccount && command.Email is { } email)
            {
                var account = await ProvisionAccountAsync(student, email, request, cancellationToken);
                accountProvisioned = account.IsSuccess;
                accountError = account.Error?.Code;
            }

            return new AdmissionOutcome(StudentMapper.ToDetail(student), accountProvisioned, accountError);
        }

        // The student must be tracked. Safe to retry after a failure.
        public async Task<Result> ProvisionAccountAsync(Student student, string email, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (student.UserId is not null)
            {
                return Result.Fail(StudentErrors.AccountAlreadyLinked);
            }

            if (!EmailAddressValidator.IsValid(email))
            {
                return Result.Fail(IdentityErrors.UserEmailInvalid);
            }

            var cleanEmail = email.Trim();
            var now = _clock.UtcNow;

            var provisioned = await _provisioner.ProvisionAsync(student.StudentNumber, cleanEmail, now, cancellationToken);

            if (!provisioned.TryGetValue(out var userId))
            {
                await AuditFailureAsync(student, provisioned.Error?.Code, request, cancellationToken);
                return Result.Fail(provisioned.Errors);
            }

            var linked = student.LinkAccount(userId, now);
            if (linked.IsFailure)
            {
                await AuditFailureAsync(student, linked.Error?.Code, request, cancellationToken);
                return linked;
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                await AuditFailureAsync(student, saved.Error?.Code, request, cancellationToken);
                return Result.Fail(saved.Errors);
            }

            var queued = _emailQueue.TryEnqueue(
                AuthEmails.AccountCreated(cleanEmail, _settings.AppName, student.StudentNumber));

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.StudentAccountCreated, _user.UserId, "Student", student.Id.ToString(), request.IpAddress, request.UserAgent, NewValues: userId.ToString()),
                cancellationToken);

            if (!queued)
            {
                await _audit.WriteAsync(new AuditEntry(AuditActions.EmailEnqueueFailed, _user.UserId, "Student", student.Id.ToString(), request.IpAddress, request.UserAgent), cancellationToken);
            }

            return Result.Ok();
        }

        private Task AuditFailureAsync(Student student, string? errorCode, RequestInfo request, CancellationToken cancellationToken) => _audit.WriteAsync(new AuditEntry(AuditActions.StudentAccountFailed, _user.UserId, "Student", student.Id.ToString(), request.IpAddress, request.UserAgent, NewValues: errorCode), cancellationToken);
    }


}
