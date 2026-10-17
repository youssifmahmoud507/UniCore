namespace UniCore.Domain.Modules.Finance
{
    public sealed record ReductionInput(ReductionKind Kind, Guid SourceId, string Name, ReductionType Type, decimal Value);



}
