using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Attendance
{
    // The attendance limits of one program OR one course (exactly one of the two ids). A course policy wins over the
    // program policy of the student. IsExamBlocking: below the minimum the student may not sit the exam (Week 12 asks).
    public sealed class AttendancePolicy : AggregateRoot
    {
        private AttendancePolicy()
        {
        }

        public Guid? AcademicProgramId { get; private set; }
        public Guid? CourseId { get; private set; }
        public decimal MinimumPercentage { get; private set; }
        public decimal WarningPercentage { get; private set; }
        public int LateThresholdMinutes { get; private set; }
        public bool IsExamBlocking { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<AttendancePolicy> Create(Guid? programId, Guid? courseId, decimal minimum, decimal warning, int lateThresholdMinutes, bool isExamBlocking, DateTimeOffset now)
        {
            if ((programId is null) == (courseId is null) || programId == Guid.Empty || courseId == Guid.Empty || !AreValid(minimum, warning, lateThresholdMinutes))
            {
                return AttendanceErrors.PolicyInvalid;
            }

            return new AttendancePolicy
            {
                Id = Guid.NewGuid(),
                AcademicProgramId = programId,
                CourseId = courseId,
                MinimumPercentage = minimum,
                WarningPercentage = warning,
                LateThresholdMinutes = lateThresholdMinutes,
                IsExamBlocking = isExamBlocking,
                IsActive = true,
                CreatedAt = now
            };
        }

        public Result Update(decimal minimum, decimal warning, int lateThresholdMinutes, bool isExamBlocking, DateTimeOffset now)
        {
            if (!AreValid(minimum, warning, lateThresholdMinutes))
            {
                return Result.Fail(AttendanceErrors.PolicyInvalid);
            }

            MinimumPercentage = minimum;
            WarningPercentage = warning;
            LateThresholdMinutes = lateThresholdMinutes;
            IsExamBlocking = isExamBlocking;
            UpdatedAt = now;
            return Result.Ok();
        }

        public void Deactivate(DateTimeOffset now)
        {
            IsActive = false;
            UpdatedAt = now;
        }

        private static bool AreValid(decimal minimum, decimal warning, int lateThresholdMinutes)
            => minimum >= 0 && minimum <= warning && warning <= 100 && lateThresholdMinutes is >= 0 and <= 180;
    }
}
