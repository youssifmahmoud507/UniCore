using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Is there already an Active structure for exactly this (program, semester) pair?
    public sealed class ActiveFeeStructureForScopeSpecification : Specification<FeeStructure>
    {
        public ActiveFeeStructureForScopeSpecification(Guid? academicProgramId, Guid? academicSemesterId, Guid excludeStructureId)
        {
            SetCriteria(s => s.Status == FeeStructureStatus.Active && s.AcademicProgramId == academicProgramId && s.AcademicSemesterId == academicSemesterId && s.Id != excludeStructureId);
        }
    }






}
