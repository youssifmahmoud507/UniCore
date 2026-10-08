using UniCore.Domain.Common.Results;

namespace UniCore.Application.Common.Abstractions
{
    public interface IUnitOfWork
    {
        Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
