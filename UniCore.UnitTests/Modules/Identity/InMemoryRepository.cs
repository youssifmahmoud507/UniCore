using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Specifications;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class InMemoryRepository<T> : IRepository<T> where T : class
    {
        public List<T> Items { get; } = new();

        // Not needed by the handlers under test.
        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<T?>(null);

        public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(specification).FirstOrDefault());

        public Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(Filter(specification));

        public Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(specification).Count);

        public Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
            => Task.FromResult(Filter(specification).Count > 0);

        public void Add(T entity) => Items.Add(entity);
        public void Update(T entity) { }
        public void Remove(T entity) => Items.Remove(entity);

        private List<T> Filter(ISpecification<T> specification)
        {
            var predicate = specification.Criteria?.Compile();
            return predicate is null ? Items.ToList() : Items.Where(predicate).ToList();
        }
    }
}
