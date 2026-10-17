namespace UniCore.Application.Modules.Finance
{
    // Invoice is set only when IssueInvoice was asked and the charge was still pending.
    public sealed record ServiceChargeOutcome(StudentChargeDto Charge, bool Created, InvoiceDto? Invoice);


}
