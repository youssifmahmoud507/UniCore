using Microsoft.EntityFrameworkCore;
using UniCore.Application.Common.Specifications;

namespace UniCore.Infrastructure.Persistence
{
    public static class SpecificationEvaluator
    {
        public static IQueryable<T> Apply<T>(IQueryable<T> source, ISpecification<T> specification, bool forCount = false) where T : class
        {
            var query = source;

            if (specification.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (specification.Criteria is not null)
            {
                query = query.Where(specification.Criteria);
            }

            if (forCount)
            {
                return query;
            }

            query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
            query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

            if (specification.OrderBy is not null)
            {
                query = query.OrderBy(specification.OrderBy);
            }
            else if (specification.OrderByDescending is not null)
            {
                query = query.OrderByDescending(specification.OrderByDescending);
            }

            if (specification.Skip is not null)
            {
                query = query.Skip(specification.Skip.Value);
            }

            if (specification.Take is not null)
            {
                query = query.Take(specification.Take.Value);
            }

            return query;
        }
    }
}
