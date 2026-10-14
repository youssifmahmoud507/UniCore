using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Every active structure that can apply to this semester (its own or "all semesters").
    public sealed class ActiveFeeStructuresForGenerationSpecification : Specification<FeeStructure>
    {
        public ActiveFeeStructuresForGenerationSpecification(Guid academicSemesterId, Guid? academicProgramId)
        {
            SetCriteria(s => s.Status == FeeStructureStatus.Active && (s.AcademicSemesterId == null || s.AcademicSemesterId == academicSemesterId) && (academicProgramId == null || s.AcademicProgramId == null || s.AcademicProgramId == academicProgramId));

            AddInclude("Items");
        }
    }

}
