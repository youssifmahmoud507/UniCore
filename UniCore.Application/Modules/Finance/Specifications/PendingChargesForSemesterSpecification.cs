using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Read only: used to find which students still have something to invoice.
    public sealed class PendingChargesForSemesterSpecification : Specification<StudentCharge>
    {
        public PendingChargesForSemesterSpecification(Guid academicSemesterId)
        {
            SetCriteria(c => c.AcademicSemesterId == academicSemesterId && c.Status == ChargeStatus.Pending);
        }
    }


}
