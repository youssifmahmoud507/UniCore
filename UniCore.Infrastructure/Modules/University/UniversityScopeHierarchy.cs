using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.University
{
    // The real hierarchy for scope checks (replaces FlatScopeHierarchy):
    //   Department -> [Department, Faculty]      Faculty -> [Faculty]
    // Section -> Department needs course sections (Week 8); until then a Section only matches itself.
    // Global is implied by the authorizer and is never listed.
    public sealed class UniversityScopeHierarchy(AppDbContext db, ILogger<UniversityScopeHierarchy> logger) : IScopeHierarchy
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<UniversityScopeHierarchy> _logger = logger;

        public async Task<IReadOnlyList<ScopeRef>> GetChainAsync(ScopeType type, Guid id, CancellationToken cancellationToken = default)
        {
            var chain = new List<ScopeRef> { new(type, id) };

            if (type != ScopeType.Department)
            {
                return chain;
            }

            try
            {
                // Nullable select: "no such department" must not look like an empty Guid.
                var facultyId = await _db.Set<Department>()
                    .AsNoTracking()
                    .Where(d => d.Id == id)
                    .Select(d => (Guid?)d.FacultyId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (facultyId is { } parent)
                {
                    chain.Add(new ScopeRef(ScopeType.Faculty, parent));
                }
            }
            catch (OperationCanceledException)
            {
                // The interface cannot return an error. A shorter chain only ever DENIES access (fail closed).
                return chain;
            }
            catch (DbException)
            {
                _logger.LogError("Could not read the department hierarchy; the scope check will use the department only.");
                return chain;
            }

            return chain;
        }
    }
}
