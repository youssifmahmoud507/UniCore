using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Caching
{
    // Cache-aside in front of UserAccessProvider (authorization.md section 8). Every authenticated request asks for the
    // user's access, so this saves four queries per request.
    //
    // Fresh enough: a change to a role, a permission, a scope, a role assignment or the active flag clears the whole
    // group (CacheInvalidationInterceptor), and the short expiry covers a failed invalidation.
    public sealed class CachedUserAccessProvider(UserAccessProvider inner, ICacheService cache) : IUserAccessProvider
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

        private readonly UserAccessProvider _inner = inner;
        private readonly ICacheService _cache = cache;

        public async Task<Result<UserAccess>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var cached = await _cache.GetOrCreateAsync(CacheGroups.UserAccess, userId.ToString("N"), async token => (await _inner.GetAsync(userId, token)).Map(CachedUserAccess.From), Lifetime, cancellationToken);

            return cached.Map(access => access.ToUserAccess());
        }
    }



}
