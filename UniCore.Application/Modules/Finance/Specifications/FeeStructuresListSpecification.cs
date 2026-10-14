using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class FeeStructuresListSpecification : Specification<FeeStructure>
    {
        public FeeStructuresListSpecification(PaginationQuery query, FeeStructureStatus? status, Guid? academicProgramId, Guid? academicSemesterId)
        {
            SetCriteria(s => (status == null || s.Status == status) && (academicProgramId == null || s.AcademicProgramId == academicProgramId) && (academicSemesterId == null || s.AcademicSemesterId == academicSemesterId));

            AddInclude("Items");
            ApplyOrderByDescending(s => s.CreatedAt);
            ApplyPaging(query);
        }
    }



}
