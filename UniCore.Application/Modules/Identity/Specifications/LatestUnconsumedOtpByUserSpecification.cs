using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class LatestUnconsumedOtpByUserSpecification : Specification<PasswordResetOtp>
    {
        public LatestUnconsumedOtpByUserSpecification(Guid userId)
        {
            SetCriteria(o => o.UserId == userId && o.ConsumedAt == null);
            ApplyOrderByDescending(o => o.CreatedAt);
            ApplyPaging(new PaginationQuery(1, 1));
            UseTracking();
        }
    }

}
