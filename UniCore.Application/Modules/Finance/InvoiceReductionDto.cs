using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record InvoiceReductionDto(Guid Id, ReductionKind Kind, Guid SourceId, string Name, ReductionType Type, decimal Value, decimal Amount);






}
