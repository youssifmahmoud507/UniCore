using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Students
{
    public sealed class StudentAccountProvisioner : IStudentAccountProvisioner
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly ILogger<StudentAccountProvisioner> _logger;

        public StudentAccountProvisioner(UserManager<ApplicationUser> users, ILogger<StudentAccountProvisioner> logger)
        {
            _users = users;
            _logger = logger;
        }

        public async Task<Result<Guid>> ProvisionAsync(
            string userName, string email, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            try
            {
                // Idempotent: a retry after a half-finished attempt reuses the account that was created.
                var existing = await _users.FindByNameAsync(userName);

                if (existing is not null)
                {
                    if (!string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
                    {
                        return StudentErrors.AccountConflict;
                    }

                    var ensured = await EnsureStudentRoleAsync(existing);
                    return ensured.IsFailure ? Result<Guid>.Fail(ensured.Errors) : existing.Id;
                }

                var created = ApplicationUser.Create(userName, email, null, now);
                if (!created.TryGetValue(out var user))
                {
                    return Result<Guid>.Fail(created.Errors);
                }

                // No password: the student sets it through the forgot-password flow.
                var createResult = (await _users.CreateAsync(user)).ToResult();
                if (createResult.IsFailure)
                {
                    return Result<Guid>.Fail(createResult.Errors);
                }

                var role = await EnsureStudentRoleAsync(user);
                if (role.IsFailure)
                {
                    // Do not leave an account without its role behind.
                    var removed = await _users.DeleteAsync(user);
                    if (!removed.Succeeded)
                    {
                        _logger.LogError("Could not remove a student account that has no role.");
                    }

                    return Result<Guid>.Fail(role.Errors);
                }

                return user.Id;
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError("Provisioning a student account failed ({ExceptionType}).", ex.GetType().Name);
                return CommonErrors.ExternalServiceUnavailable;
            }
        }

        private async Task<Result> EnsureStudentRoleAsync(ApplicationUser user)
        {
            if (await _users.IsInRoleAsync(user, RoleNames.Student))
            {
                return Result.Ok();
            }

            return (await _users.AddToRoleAsync(user, RoleNames.Student)).ToResult();
        }
    }
}
