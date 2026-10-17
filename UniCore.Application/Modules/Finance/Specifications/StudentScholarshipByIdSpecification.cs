using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // ---------- awards ----------

    public sealed class StudentScholarshipByIdSpecification : Specification<StudentScholarship>
    {
        public StudentScholarshipByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(a => a.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }






}
