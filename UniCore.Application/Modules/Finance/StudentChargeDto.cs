using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record StudentChargeDto(Guid Id, Guid StudentId, Guid AcademicSemesterId, Guid FeeDefinitionId, string Description, decimal Amount, DateOnly DueDate, ChargeStatus Status, Guid? InvoiceId, DateTimeOffset CreatedAt);



}
