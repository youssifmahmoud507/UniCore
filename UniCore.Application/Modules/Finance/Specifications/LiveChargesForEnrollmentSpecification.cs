using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // The live (not cancelled) charges that were made for one enrollment.
    public sealed class LiveChargesForEnrollmentSpecification : Specification<StudentCharge>
    {
        public LiveChargesForEnrollmentSpecification(Guid enrollmentId, bool tracking = false)
        {
            SetCriteria(c => c.EnrollmentId == enrollmentId && c.Status != ChargeStatus.Cancelled);

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
