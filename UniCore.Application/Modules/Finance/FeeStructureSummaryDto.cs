using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record FeeStructureSummaryDto(Guid Id, Guid? AcademicProgramId, Guid? AcademicSemesterId, string Name, FeeStructureStatus Status, int ItemCount, decimal Total);

}
