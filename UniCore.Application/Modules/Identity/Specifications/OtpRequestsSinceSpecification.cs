using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class OtpRequestsSinceSpecification : Specification<PasswordResetOtp>
    {
        public OtpRequestsSinceSpecification(Guid userId, DateTimeOffset since)
        {
            SetCriteria(o => o.UserId == userId && o.CreatedAt >= since);
        }
    }

}
