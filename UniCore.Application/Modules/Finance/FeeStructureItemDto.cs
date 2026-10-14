namespace UniCore.Application.Modules.Finance
{
    public sealed record FeeStructureItemDto(Guid Id, Guid FeeDefinitionId, decimal Amount);

}
