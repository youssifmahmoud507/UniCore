using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students.Specifications
{
    public sealed class StudentByPersonSpecification : Specification<Student>
    {
        public StudentByPersonSpecification(Guid personProfileId)
        {
            SetCriteria(s => s.PersonProfileId == personProfileId);
        }
    }


}
