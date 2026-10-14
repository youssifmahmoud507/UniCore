using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students.Specifications
{
    public sealed class StudentByUserSpecification : Specification<Student>
    {
        public StudentByUserSpecification(Guid userId)
        {
            SetCriteria(s => s.UserId == userId);
            AddInclude("Profile");
            AddInclude("ProgramHistory");
            AddInclude("StatusHistory");
        }
    }


}
