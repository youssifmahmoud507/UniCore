using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentRequests
{
    // A student's request for a leave of absence. The LEAVE_REQUEST workflow (department head, dean) approves it; when it
    // completes the student's status becomes OnLeave (effective on the approval day). Coming back is a status change by the
    // registrar; ExpectedReturnDate only tells them when.
    public sealed class LeaveRequest : StudentRequest
    {
        private LeaveRequest()
        {
        }

        public DateOnly StartDate { get; private set; }
        public DateOnly ExpectedReturnDate { get; private set; }

        public static Result<LeaveRequest> Create(Guid studentId, DateOnly startDate, DateOnly expectedReturnDate, string? reason, Guid requestedByUserId, DateTimeOffset now)
        {
            var clean = CleanReason(reason);

            if (studentId == Guid.Empty || requestedByUserId == Guid.Empty || clean is null || expectedReturnDate <= startDate)
            {
                return StudentRequestErrors.LeaveDataInvalid;
            }

            return new LeaveRequest
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                StartDate = startDate,
                ExpectedReturnDate = expectedReturnDate,
                Reason = clean,
                Status = AcademicRequestStatus.Pending,
                RequestedByUserId = requestedByUserId,
                RequestedAt = now
            };
        }
    }
}
