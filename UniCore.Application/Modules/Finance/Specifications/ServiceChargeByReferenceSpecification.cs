using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // The live (not cancelled) service charge of a student for one request.
    public sealed class ServiceChargeByReferenceSpecification : Specification<StudentCharge>
    {
        public ServiceChargeByReferenceSpecification(Guid studentId, string reference)
        {
            SetCriteria(c => c.StudentId == studentId && c.ServiceReference == reference && c.Status != ChargeStatus.Cancelled);
        }
    }


}
