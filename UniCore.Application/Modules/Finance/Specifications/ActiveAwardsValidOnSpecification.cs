using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // The awards that apply to an invoice issued on this date.
    public sealed class ActiveAwardsValidOnSpecification : Specification<StudentScholarship>
    {
        public ActiveAwardsValidOnSpecification(Guid studentId, DateOnly date)
        {
            SetCriteria(a => a.StudentId == studentId && a.Status == StudentScholarshipStatus.Active && a.ValidFrom <= date && (a.ValidTo == null || a.ValidTo >= date));
        }
    }






}
