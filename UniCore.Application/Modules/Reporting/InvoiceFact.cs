using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Reporting
{
    // The small, flat rows the reports are calculated from. The data source reads them from the database; the reports
    // never touch entities, so every report can be tested with plain lists.

    public sealed record InvoiceFact(Guid StudentId, string StudentNumber, DateOnly IssueDate, DateOnly DueDate, InvoiceStatus Status, decimal Total, decimal Paid, decimal Balance);




}
