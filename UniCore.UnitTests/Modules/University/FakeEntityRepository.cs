using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Common.Entities;

namespace UniCore.UnitTests.Modules.University
{
    // In-memory repository for the University module's aggregates. Unlike the Identity fake,
    // GetByIdAsync really finds the entity, because the handlers load by id.
    internal sealed class FakeEntityRepository<T> : IRepository<T> where T : BaseEntity
    {
        public List<T> Items { get; } = [];

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<T?>(Items.FirstOrDefault(e => e.Id == id));

        public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
            => Task.FromResult<T?>(Filter(specification).FirstOrDefault());

        // The real repository also applies ordering and paging; here only the filter matters, and the
        // handlers' paging is checked through the totals.
        public Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            var filtered = Filter(specification);

            if (specification.Skip is { } skip)
            {
                filtered = [.. filtered.Skip(skip)];
            }

            if (specification.Take is { } take)
            {
                filtered = [.. filtered.Take(take)];
            }

            return Task.FromResult<IReadOnlyList<T>>(filtered);
        }

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
            return predicate is null ? [.. Items] : [.. Items.Where(predicate)];
        }
    }
}
