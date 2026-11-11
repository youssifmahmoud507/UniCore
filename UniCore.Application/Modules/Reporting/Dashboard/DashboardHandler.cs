using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed class DashboardHandler(IPermissionAuthorizer authorizer, IEnumerable<IDashboardWidget> widgets, ICacheService cache, ICurrentUser user, IClock clock)
    {
        // Short: a dashboard is a glance, and nothing clears this cache when the data changes.
        public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IReadOnlyList<IDashboardWidget> _widgets = [.. widgets.OrderBy(w => w.Category, StringComparer.Ordinal).ThenBy(w => w.Code, StringComparer.Ordinal)];
        private readonly ICacheService _cache = cache;
        private readonly ICurrentUser _user = user;
        private readonly IClock _clock = clock;

        public async Task<Result<DashboardDto>> GetAsync(CancellationToken cancellationToken = default)
        {
            var read = await _authorizer.AuthorizeAsync(Permissions.Reports.Read, null, cancellationToken);

            if (read.IsFailure || _user.UserId is not { } userId)
            {
                return Result<DashboardDto>.Fail(read.IsFailure ? read.Errors : [Error.Unauthorized()]);
            }

            var shown = new List<DashboardWidgetDto>();
            var unavailable = new List<string>();

            foreach (var widget in _widgets)
            {
                if ((await _authorizer.AuthorizeAsync(widget.Permission, null, cancellationToken)).IsFailure)
                {
                    continue;
                }

                var key = widget.IsPerUser ? $"{widget.Code}:{userId:N}" : widget.Code;
                var loaded = await _cache.GetOrCreateAsync(
                    CacheGroups.Dashboard,
                    key,
                    async token => (await widget.LoadAsync(token)).Map(metrics => new DashboardSnapshot(metrics, _clock.UtcNow)),
                    CacheLifetime,
                    cancellationToken);

                if (loaded.TryGetValue(out var snapshot))
                {
                    shown.Add(new DashboardWidgetDto(widget.Code, widget.Title, widget.Category, snapshot.Metrics, snapshot.GeneratedAt));
                }
                else
                {
                    unavailable.Add(widget.Code);
                }
            }

            return new DashboardDto(shown, unavailable);
        }
    }


}
