using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Error? FailWith { get; set; }
        public int SaveCalls { get; private set; }

        public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(FailWith is null ? Result<int>.Ok(1) : Result<int>.Fail(FailWith));
        }
    }
}
