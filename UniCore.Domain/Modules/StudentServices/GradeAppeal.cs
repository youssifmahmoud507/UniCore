using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    // A student's appeal against a published grade of their own. The workflow (GRADE_APPEAL: department head, then dean) decides
    // whether the appeal is justified. An approved appeal changes no score by itself: the instructor then files a grade change
    // request (GradeChanges.Request, the GRADE_CHANGE workflow), the only way a final grade changes.
    public sealed class GradeAppeal : ServiceRequest
    {
        private GradeAppeal()
        {
        }

        public Guid GradeId { get; private set; }
        public string Reason { get; private set; } = string.Empty;

        public static Result<GradeAppeal> Create(Guid studentId, Guid requestedByUserId, Guid gradeId, string? reason, DateTimeOffset now)
        {
            if (gradeId == Guid.Empty)
            {
                return ServiceRequestErrors.Invalid;
            }

            var text = RequiredText(reason, TextMaxLength);
            if (!text.TryGetValue(out var validReason))
            {
                return Result<GradeAppeal>.Fail(text.Errors);
            }

            var appeal = new GradeAppeal { GradeId = gradeId, Reason = validReason };
            var started = appeal.Initialize(studentId, requestedByUserId, now);

            return started.IsFailure ? Result<GradeAppeal>.Fail(started.Errors) : appeal;
        }
    }
}
