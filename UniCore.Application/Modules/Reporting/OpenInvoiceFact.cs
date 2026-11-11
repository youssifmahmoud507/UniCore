namespace UniCore.Application.Modules.Reporting
{
    // Open invoices only (what is still owed): for the finance dashboard.
    public sealed record OpenInvoiceFact(DateOnly DueDate, decimal Balance);




}
