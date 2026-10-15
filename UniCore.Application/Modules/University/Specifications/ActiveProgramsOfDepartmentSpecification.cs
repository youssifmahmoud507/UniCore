using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class ActiveProgramsOfDepartmentSpecification : Specification<AcademicProgram>
    {
        public ActiveProgramsOfDepartmentSpecification(Guid departmentId)
        {
            SetCriteria(p => p.DepartmentId == departmentId && p.IsActive);
        }
    }
}
