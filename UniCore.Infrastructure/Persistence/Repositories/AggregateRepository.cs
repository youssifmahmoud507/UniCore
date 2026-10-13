using UniCore.Application.Common.Repositories;
using UniCore.Domain.Common.Entities;

namespace UniCore.Infrastructure.Persistence.Repositories
{
    public sealed class AggregateRepository<T>(AppDbContext db) : GenericRepository<T>(db), IAggregateRepository<T> where T : AggregateRoot
    {
    }
}
