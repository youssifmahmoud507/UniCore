using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakePasswordResetAccountService : IPasswordResetAccountService
    {
        public Dictionary<string, Guid> Accounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Result ValidateResult { get; set; } = Result.Ok();
        public Result ApplyResult { get; set; } = Result.Ok();
        public int ApplyCalls { get; private set; }

        public Task<Result<ResetCandidate>> FindActiveByEmailAsync(string email)
            => Task.FromResult(Accounts.TryGetValue(email, out var id)
                ? Result<ResetCandidate>.Ok(new ResetCandidate(id, email))
                : Result<ResetCandidate>.Fail(Error.NotFound("USER_NOT_FOUND", "User not found.")));

        public Task<Result<string>> GenerateResetTokenAsync(Guid userId)
            => Task.FromResult(Result<string>.Ok("reset-token"));

        public Task<Result> ValidateResetAsync(Guid userId, string resetToken, string newPassword)
            => Task.FromResult(ValidateResult);

        public Task<Result> ApplyResetAsync(Guid userId, string resetToken, string newPassword)
        {
            ApplyCalls++;
            return Task.FromResult(ApplyResult);
        }
    }



}
