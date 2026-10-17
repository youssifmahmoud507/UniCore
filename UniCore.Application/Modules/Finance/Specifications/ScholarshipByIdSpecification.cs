using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // ---------- scholarships ----------

    public sealed class ScholarshipByIdSpecification : Specification<Scholarship>
    {
        public ScholarshipByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(s => s.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }






}
