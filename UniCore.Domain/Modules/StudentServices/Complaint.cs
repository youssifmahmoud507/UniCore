using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    // A student's complaint. The workflow decides it; the answer is the decision note.
    public sealed class Complaint : ServiceRequest
    {
        private Complaint()
        {
        }

        public ComplaintCategory Category { get; private set; }
        public string Subject { get; private set; } = string.Empty;
        public string Details { get; private set; } = string.Empty;

        public static Result<Complaint> Create(Guid studentId, Guid requestedByUserId, ComplaintCategory category, string? subject, string? details, DateTimeOffset now)
        {
            if (!Enum.IsDefined(category))
            {
                return ServiceRequestErrors.Invalid;
            }

            var title = RequiredText(subject, ShortTextMaxLength);
            var body = RequiredText(details, TextMaxLength);
            if (!title.TryGetValue(out var validSubject) || !body.TryGetValue(out var validDetails))
            {
                return Result<Complaint>.Fail(title.IsFailure ? title.Errors : body.Errors);
            }

            var complaint = new Complaint { Category = category, Subject = validSubject, Details = validDetails };
            var started = complaint.Initialize(studentId, requestedByUserId, now);

            return started.IsFailure ? Result<Complaint>.Fail(started.Errors) : complaint;
        }
    }
}
