using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Students
{
    // Aggregate root. A student is NOT a login account: UserId only links to one (optional).
    // There is no ProgramId here: the program lives in StudentProgramHistory.
    // Load it with Profile, ProgramHistory and StatusHistory before calling the behaviour methods.
    public sealed class Student : AggregateRoot
    {
        private const int MaxNumberLength = 30;
        private const int MaxReasonLength = 500;

        private static readonly DateOnly MinDate = new(1900, 1, 1);

        private readonly List<StudentProgramHistory> _programHistory = [];
        private readonly List<StudentStatusHistory> _statusHistory = [];

        private Student()
        {
        }

        public Guid PersonProfileId { get; private set; }
        public Guid? UserId { get; private set; }
        public string StudentNumber { get; private set; } = string.Empty;
        public DateOnly AdmissionDate { get; private set; }
        public StudentAcademicStatus AcademicStatus { get; private set; }
        public DateOnly? ExpectedGraduationDate { get; private set; }
        public DateOnly? GraduationDate { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public StudentAcademicProfile? Profile { get; private set; }

        public IReadOnlyList<StudentProgramHistory> ProgramHistory => _programHistory;

        public IReadOnlyList<StudentStatusHistory> StatusHistory => _statusHistory;

        public StudentProgramHistory? CurrentProgram => _programHistory.FirstOrDefault(h => h.IsOpen);

        public static Result<Student> Create(Guid personProfileId, string? studentNumber, DateOnly admissionDate, DateOnly? expectedGraduationDate, Guid admissionAcademicYearId, Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId, DateTimeOffset now)
        {
            var number = studentNumber?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(number)
                || number.Length > MaxNumberLength
                || !number.All(char.IsAsciiLetterOrDigit))
            {
                return StudentErrors.NumberInvalid;
            }

            if (personProfileId == Guid.Empty
                || admissionAcademicYearId == Guid.Empty
                || academicProgramId == Guid.Empty
                || studyPlanVersionId == Guid.Empty
                || (academicLevelId is { } level && level == Guid.Empty)
                || admissionDate < MinDate
                || (expectedGraduationDate is { } expected && expected < admissionDate))
            {
                return StudentErrors.DataInvalid;
            }

            var id = Guid.NewGuid();

            var student = new Student
            {
                Id = id,
                PersonProfileId = personProfileId,
                StudentNumber = number,
                AdmissionDate = admissionDate,
                AcademicStatus = StudentAcademicStatus.Active,
                ExpectedGraduationDate = expectedGraduationDate,
                CreatedAt = now,
                Profile = StudentAcademicProfile.Create(id, admissionAcademicYearId, academicLevelId, studyPlanVersionId)
            };
            student._programHistory.Add(StudentProgramHistory.Open(id, academicProgramId, studyPlanVersionId, academicLevelId, admissionDate, null));

            student.RaiseDomainEvent(new StudentAdmitted(Guid.NewGuid(), now, id, personProfileId, number, academicProgramId, studyPlanVersionId, admissionAcademicYearId, admissionDate));

            return student;
        }

        // Idempotent for the same account; a different account is a conflict.
        public Result LinkAccount(Guid userId, DateTimeOffset now)
        {
            if (userId == Guid.Empty)
            {
                return Result.Fail(StudentErrors.DataInvalid);
            }

            if (UserId is { } existing)
            {
                return existing == userId ? Result.Ok() : Result.Fail(StudentErrors.AccountAlreadyLinked);
            }

            UserId = userId;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result ChangeStatus(StudentAcademicStatus newStatus, DateOnly effectiveDate, string? reason, Guid changedByUserId, DateOnly today, DateTimeOffset now)
        {
            if (!Enum.IsDefined(newStatus) || changedByUserId == Guid.Empty)
            {
                return Result.Fail(StudentErrors.DataInvalid);
            }

            if (newStatus == AcademicStatus)
            {
                return Result.Fail(StudentErrors.StatusUnchanged);
            }

            if (!StudentStatusRules.CanTransition(AcademicStatus, newStatus))
            {
                return Result.Fail(StudentErrors.StatusTransitionInvalid);
            }

            var cleanReason = NormalizeReason(reason);

            if (cleanReason is { Length: > MaxReasonLength })
            {
                return Result.Fail(StudentErrors.DataInvalid);
            }

            if (StudentStatusRules.RequiresReason(newStatus) && cleanReason is null)
            {
                return Result.Fail(StudentErrors.StatusReasonRequired);
            }

            var lastEffective = _statusHistory.Count == 0 ? AdmissionDate : _statusHistory.Max(h => h.EffectiveDate);

            // One day of tolerance for time zones that are ahead of UTC.
            if (effectiveDate < AdmissionDate || effectiveDate < lastEffective || effectiveDate > today.AddDays(1))
            {
                return Result.Fail(StudentErrors.EffectiveDateInvalid);
            }

            var oldStatus = AcademicStatus;

            _statusHistory.Add(StudentStatusHistory.Record(Id, oldStatus, newStatus, effectiveDate, cleanReason, changedByUserId, now));

            AcademicStatus = newStatus;
            UpdatedAt = now;

            if (newStatus == StudentAcademicStatus.Graduated)
            {
                GraduationDate = effectiveDate;
                CurrentProgram?.Close(effectiveDate, StudentProgramStatus.Completed);
            }
            else if (newStatus == StudentAcademicStatus.Dismissed)
            {
                CurrentProgram?.Close(effectiveDate, StudentProgramStatus.Discontinued);
            }

            return Result.Ok();
        }

        public Result ChangeProgram(Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId, DateOnly startDate, string? reason, DateOnly today, DateTimeOffset now)
        {
            if (academicProgramId == Guid.Empty
                || studyPlanVersionId == Guid.Empty
                || (academicLevelId is { } level && level == Guid.Empty))
            {
                return Result.Fail(StudentErrors.DataInvalid);
            }

            if (AcademicStatus != StudentAcademicStatus.Active)
            {
                return Result.Fail(StudentErrors.NotActive);
            }

            if (Profile is null)
            {
                return Result.Fail(StudentErrors.ProfileMissing);
            }

            var current = CurrentProgram;
            if (current is null)
            {
                return Result.Fail(StudentErrors.ProgramMissing);
            }

            var cleanReason = NormalizeReason(reason);

            if (cleanReason is null)
            {
                return Result.Fail(StudentErrors.ProgramChangeReasonRequired);
            }

            if (cleanReason.Length > MaxReasonLength)
            {
                return Result.Fail(StudentErrors.DataInvalid);
            }

            if (current.AcademicProgramId == academicProgramId
                && current.StudyPlanVersionId == studyPlanVersionId
                && current.AcademicLevelId == academicLevelId)
            {
                return Result.Fail(StudentErrors.ProgramUnchanged);
            }

            if (startDate < current.StartDate || startDate > today.AddDays(1))
            {
                return Result.Fail(StudentErrors.EffectiveDateInvalid);
            }

            current.Close(startDate, StudentProgramStatus.Changed);

            _programHistory.Add(StudentProgramHistory.Open(
                Id, academicProgramId, studyPlanVersionId, academicLevelId, startDate, cleanReason));

            Profile.SetPlacement(academicLevelId, studyPlanVersionId);
            UpdatedAt = now;

            return Result.Ok();
        }

        // Used when the academic engine moves a student to another level or recalculates the standing.
        public Result UpdateAcademicProfile(Guid? academicLevelId, AcademicStanding? standing, DateTimeOffset now)
        {
            if ((academicLevelId is null && standing is null)
                || (academicLevelId is { } level && level == Guid.Empty)
                || (standing is { } value && !Enum.IsDefined(value)))
            {
                return Result.Fail(StudentErrors.DataInvalid);
            }

            if (StudentStatusRules.IsTerminal(AcademicStatus))
            {
                return Result.Fail(StudentErrors.NotActive);
            }

            if (Profile is null)
            {
                return Result.Fail(StudentErrors.ProfileMissing);
            }

            if (academicLevelId is { } newLevel)
            {
                Profile.SetLevel(newLevel);
            }

            if (standing is { } newStanding)
            {
                Profile.SetStanding(newStanding);
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        private static string? NormalizeReason(string? reason)
        {
            var clean = reason?.Trim();
            return string.IsNullOrEmpty(clean) ? null : clean;
        }
    }
}
