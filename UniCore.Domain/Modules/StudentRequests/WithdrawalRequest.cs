using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentRequests
{
    // A student's request to withdraw from one course (one enrollment). The WITHDRAWAL_REQUEST workflow (department head, dean,
    // registrar) approves it; when it completes the enrollment is withdrawn.
    public sealed class WithdrawalRequest : StudentRequest
    {
        private WithdrawalRequest()
        {
        }

        public Guid RegistrationId { get; private set; }
        public Guid EnrollmentId { get; private set; }
        public Guid CourseSectionId { get; private set; }

        public static Result<WithdrawalRequest> Create(Guid studentId, Guid registrationId, Guid enrollmentId, Guid courseSectionId, string? reason, Guid requestedByUserId, DateTimeOffset now)
        {
            var clean = CleanReason(reason);

            if (studentId == Guid.Empty || registrationId == Guid.Empty || enrollmentId == Guid.Empty || courseSectionId == Guid.Empty || requestedByUserId == Guid.Empty || clean is null)
            {
                return StudentRequestErrors.WithdrawalDataInvalid;
            }

            return new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                RegistrationId = registrationId,
                EnrollmentId = enrollmentId,
                CourseSectionId = courseSectionId,
                Reason = clean,
                Status = AcademicRequestStatus.Pending,
                RequestedByUserId = requestedByUserId,
                RequestedAt = now
            };
        }
    }
}
