using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Finance
{
    // Invoices every student that has pending charges in the semester. Each student runs in its own DI scope,
    // so one failed save cannot spoil the next student.
    public interface IInvoiceBatchRunner
    {
        Task<Result<BulkInvoiceResult>> RunAsync(Guid academicSemesterId, DateOnly? dueDate, CancellationToken cancellationToken = default);
    }

}
