using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Modules.Notifications;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Notifications
{
    public sealed class UserContactLookup(AppDbContext db, ILogger<UserContactLookup> logger) : IUserContactLookup
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<UserContactLookup> _logger = logger;

        public async Task<string?> GetEmailAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var email = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId && u.IsActive)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(cancellationToken);

                return string.IsNullOrWhiteSpace(email) ? null : email;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Looking up a user's email failed ({ExceptionType}).", ex.GetType().Name);
                return null;
            }
        }
    }

}
