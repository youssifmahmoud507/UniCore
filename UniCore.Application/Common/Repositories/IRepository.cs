using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Specifications;

namespace UniCore.Application.Common.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
        Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
        Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

        void Add(T entity);
        void Update(T entity);
        void Remove(T entity);
    }
}
