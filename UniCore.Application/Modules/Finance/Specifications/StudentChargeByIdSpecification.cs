using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class StudentChargeByIdSpecification : Specification<StudentCharge>
    {
        public StudentChargeByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(c => c.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

}
