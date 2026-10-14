using UniCore.Domain.Common.Entities;

namespace UniCore.Application.Common.Repositories
{
    // Repositories exist only for aggregate roots. New aggregates inject this one.
    // IRepository<T> stays for the few non-aggregate entities we still load directly.
    public interface IAggregateRepository<T> : IRepository<T> where T : AggregateRoot
    {
    }
}
