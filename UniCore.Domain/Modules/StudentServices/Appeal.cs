using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    // A student's appeal against a decision (standing, dismissal, a disciplinary action...). A grade has its own request, GradeAppeal.
    public sealed class Appeal : ServiceRequest
    {
        private Appeal()
        {
        }

        public AppealTarget Target { get; private set; }
        public string Subject { get; private set; } = string.Empty;
        public string Reason { get; private set; } = string.Empty;

        public static Result<Appeal> Create(Guid studentId, Guid requestedByUserId, AppealTarget target, string? subject, string? reason, DateTimeOffset now)
        {
            if (!Enum.IsDefined(target))
            {
                return ServiceRequestErrors.Invalid;
            }

            var title = RequiredText(subject, ShortTextMaxLength);
            var body = RequiredText(reason, TextMaxLength);
            if (!title.TryGetValue(out var validSubject) || !body.TryGetValue(out var validReason))
            {
                return Result<Appeal>.Fail(title.IsFailure ? title.Errors : body.Errors);
            }

            var appeal = new Appeal { Target = target, Subject = validSubject, Reason = validReason };
            var started = appeal.Initialize(studentId, requestedByUserId, now);

            return started.IsFailure ? Result<Appeal>.Fail(started.Errors) : appeal;
        }
    }
}
