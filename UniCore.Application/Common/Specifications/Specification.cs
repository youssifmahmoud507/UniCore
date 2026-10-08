using System.Linq.Expressions;
using UniCore.Application.Common.Pagination;

namespace UniCore.Application.Common.Specifications
{
    public abstract class Specification<T> : ISpecification<T> where T : class
    {
        private readonly List<Expression<Func<T, object>>> _includes = [];
        private readonly List<string> _includeStrings = [];

        public Expression<Func<T, bool>>? Criteria { get; private set; }
        public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;
        public IReadOnlyList<string> IncludeStrings => _includeStrings;
        public Expression<Func<T, object>>? OrderBy { get; private set; }
        public Expression<Func<T, object>>? OrderByDescending { get; private set; }
        public int? Skip { get; private set; }
        public int? Take { get; private set; }

        public bool AsNoTracking { get; private set; } = true;

        protected void SetCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;
        protected void AddInclude(Expression<Func<T, object>> include) => _includes.Add(include);
        protected void AddInclude(string includePath) => _includeStrings.Add(includePath);
        protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;
        protected void ApplyOrderByDescending(Expression<Func<T, object>> orderBy) => OrderByDescending = orderBy;
        protected void UseTracking() => AsNoTracking = false;

        protected void ApplyPaging(PaginationQuery query)
        {
            Skip = query.Skip;
            Take = query.NormalizedPageSize;
        }
    }
}
