namespace UniCore.Application.Modules.Finance
{
    public sealed record InvoiceLineDto(Guid Id, string Description, int Quantity, decimal UnitPrice, decimal Amount, Guid? StudentChargeId);



}
