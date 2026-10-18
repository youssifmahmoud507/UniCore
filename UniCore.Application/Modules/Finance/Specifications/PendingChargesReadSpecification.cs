using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class PendingChargesReadSpecification : Specification<StudentCharge>
    {
        public PendingChargesReadSpecification(Guid studentId)
        {
            SetCriteria(c => c.StudentId == studentId && c.Status == ChargeStatus.Pending);
        }
    }

}
