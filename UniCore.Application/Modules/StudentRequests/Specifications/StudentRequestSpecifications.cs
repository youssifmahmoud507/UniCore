using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.StudentRequests;

namespace UniCore.Application.Modules.StudentRequests.Specifications
{
    public sealed class WithdrawalByIdSpecification : Specification<WithdrawalRequest>
    {
        public WithdrawalByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(r => r.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class PendingWithdrawalOfEnrollmentSpecification : Specification<WithdrawalRequest>
    {
        public PendingWithdrawalOfEnrollmentSpecification(Guid enrollmentId)
        {
            SetCriteria(r => r.EnrollmentId == enrollmentId && r.Status == AcademicRequestStatus.Pending);
        }
    }

    public sealed class WithdrawalsOfRequesterSpecification : Specification<WithdrawalRequest>
    {
        public WithdrawalsOfRequesterSpecification(Guid userId)
        {
            SetCriteria(r => r.RequestedByUserId == userId);
            ApplyOrderByDescending(r => r.RequestedAt);
        }
    }

    public sealed class LeaveByIdSpecification : Specification<LeaveRequest>
    {
        public LeaveByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(r => r.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class PendingLeaveOfStudentSpecification : Specification<LeaveRequest>
    {
        public PendingLeaveOfStudentSpecification(Guid studentId)
        {
            SetCriteria(r => r.StudentId == studentId && r.Status == AcademicRequestStatus.Pending);
        }
    }

    public sealed class LeavesOfRequesterSpecification : Specification<LeaveRequest>
    {
        public LeavesOfRequesterSpecification(Guid userId)
        {
            SetCriteria(r => r.RequestedByUserId == userId);
            ApplyOrderByDescending(r => r.RequestedAt);
        }
    }
}
