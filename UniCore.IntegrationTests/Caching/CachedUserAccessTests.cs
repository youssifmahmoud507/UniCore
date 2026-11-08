using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Modules.Caching;

namespace UniCore.IntegrationTests.Caching
{
    public class CachedUserAccessTests
    {
        private static UserAccess Sample() => new(Guid.NewGuid(), [new RoleGrant("Registrar",new HashSet<string>(["Users.Read", "Students.Read"], StringComparer.Ordinal),[new ScopeGrant(ScopeType.Department, Guid.NewGuid()), new ScopeGrant(ScopeType.Global, null)]),new RoleGrant("Student", new HashSet<string>(["Me.Read"], StringComparer.Ordinal), [])
            ]);

        [Fact]
        public void Converting_to_the_cache_shape_and_back_loses_nothing()
        {
            var access = Sample();

            var back = CachedUserAccess.From(access).ToUserAccess();

            Assert.Equal(access.UserId, back.UserId);
            Assert.Equal(2, back.Grants.Count);

            var registrar = back.Grants[0];
            Assert.Equal("Registrar", registrar.Role);
            Assert.Equal(2, registrar.Permissions.Count);
            Assert.Contains("Users.Read", registrar.Permissions);
            Assert.Equal(ScopeType.Department, registrar.Scopes[0].ScopeType);
            Assert.Equal(access.Grants[0].Scopes[0].ScopeId, registrar.Scopes[0].ScopeId);
            Assert.Equal(ScopeType.Global, registrar.Scopes[1].ScopeType);
            Assert.Null(registrar.Scopes[1].ScopeId);

            Assert.Empty(back.Grants[1].Scopes);
        }

        [Fact]
        public async Task A_user_access_survives_the_real_cache_round_trip_with_case_sensitive_permissions()
        {
            var cache = new DistributedCacheService(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                NullLogger<DistributedCacheService>.Instance);
            var access = Sample();

            await cache.GetOrCreateAsync("access", "user", _ => Task.FromResult(Result<CachedUserAccess>.Ok(CachedUserAccess.From(access))), TimeSpan.FromMinutes(1));
            var hit = await cache.GetOrCreateAsync<CachedUserAccess>("access", "user", _ => Task.FromResult(Result<CachedUserAccess>.Fail(Error.Failure())), TimeSpan.FromMinutes(1));

            Assert.True(hit.IsSuccess);

            var back = hit.Value!.ToUserAccess();
            var registrar = back.Grants[0];

            Assert.Equal(access.UserId, back.UserId);
            Assert.True(registrar.Permissions.Contains("Users.Read"));
            Assert.False(registrar.Permissions.Contains("users.read"));
            Assert.Equal(2, registrar.Scopes.Count);
            Assert.Equal(ScopeType.Department, registrar.Scopes[0].ScopeType);
            Assert.Null(registrar.Scopes[1].ScopeId);
        }

        [Fact]
        public void A_user_without_roles_converts_too()
        {
            var empty = new UserAccess(Guid.NewGuid(), []);

            var back = CachedUserAccess.From(empty).ToUserAccess();

            Assert.Equal(empty.UserId, back.UserId);
            Assert.Empty(back.Grants);
        }
    }
}
