using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeUserAccountService : IUserAccountService
    {
        public Result<TokenSubject> ValidateResult { get; set; } = Result<TokenSubject>.Fail(IdentityErrors.InvalidCredentials);
        public Result<TokenSubject> ActiveResult { get; set; } = Result<TokenSubject>.Fail(IdentityErrors.AccountInactive);
        public int ValidateCalls { get; private set; }

        public Task<Result<TokenSubject>> ValidateCredentialsAsync(string login, string password)
        {
            ValidateCalls++;
            return Task.FromResult(ValidateResult);
        }

        public Task<Result<TokenSubject>> GetActiveSubjectAsync(Guid userId) => Task.FromResult(ActiveResult);
    }
}
