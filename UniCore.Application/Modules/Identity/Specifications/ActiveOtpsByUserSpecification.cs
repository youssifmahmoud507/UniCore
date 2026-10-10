using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class ActiveOtpsByUserSpecification : Specification<PasswordResetOtp>
    {
        public ActiveOtpsByUserSpecification(Guid userId, DateTimeOffset now)
        {
            SetCriteria(o => o.UserId == userId && o.ConsumedAt == null && o.ExpiresAt > now);
            UseTracking();
        }
    }

}
