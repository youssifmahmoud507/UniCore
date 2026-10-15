using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class ActiveDepartmentsOfFacultySpecification : Specification<Department>
    {
        public ActiveDepartmentsOfFacultySpecification(Guid facultyId)
        {
            SetCriteria(d => d.FacultyId == facultyId && d.IsActive);
        }
    }
}
