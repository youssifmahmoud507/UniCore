using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record FeeStructureDto(Guid Id, Guid? AcademicProgramId, Guid? AcademicSemesterId, string Name, FeeStructureStatus Status, decimal Total, IReadOnlyList<FeeStructureItemDto> Items, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? ArchivedAt);

}
