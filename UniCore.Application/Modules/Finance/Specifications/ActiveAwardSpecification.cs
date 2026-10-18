using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class ActiveAwardSpecification : Specification<StudentScholarship>
    {
        public ActiveAwardSpecification(Guid studentId, Guid scholarshipId)
        {
            SetCriteria(a => a.StudentId == studentId && a.ScholarshipId == scholarshipId && a.Status == StudentScholarshipStatus.Active);
        }
    }






}
