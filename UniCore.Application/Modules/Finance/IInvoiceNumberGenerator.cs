using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Finance
{
    public interface IInvoiceNumberGenerator
    {
        // Concurrency safe. Gaps are possible (a number is lost if the save that follows fails).
        Task<Result<string>> NextAsync(int year, CancellationToken cancellationToken = default);
    }

}
