using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class ActiveRefreshTokensByUserSpecification : Specification<RefreshToken>
    {
        public ActiveRefreshTokensByUserSpecification(Guid userId, DateTimeOffset now)
        {
            SetCriteria(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now);
            UseTracking();
        }
    }

}
