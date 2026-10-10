using Microsoft.EntityFrameworkCore;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Specifications;

namespace UniCore.Infrastructure.Persistence.Repositories
{
    public class GenericRepository<T>(AppDbContext db) : IRepository<T> where T : class
    {
        protected AppDbContext Db { get; } = db;
        protected DbSet<T> Set => Db.Set<T>();

        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await Set.FindAsync([id], cancellationToken);
        public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => SpecificationEvaluator.Apply(Set, specification).FirstOrDefaultAsync(cancellationToken);
        public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => await SpecificationEvaluator.Apply(Set, specification).ToListAsync(cancellationToken);
        public Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => SpecificationEvaluator.Apply(Set, specification, forCount: true).CountAsync(cancellationToken);
        public Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => SpecificationEvaluator.Apply(Set, specification, forCount: true).AnyAsync(cancellationToken);
        public void Add(T entity) => Set.Add(entity);
        public void Update(T entity) => Set.Update(entity);
        public void Remove(T entity) => Set.Remove(entity);
    }
}
