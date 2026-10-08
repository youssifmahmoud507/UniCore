using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace UniCore.Application.Common.Specifications
{
    public interface ISpecification<T> where T : class
    {
        Expression<Func<T, bool>>? Criteria { get; }
        IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
        IReadOnlyList<string> IncludeStrings { get; }
        Expression<Func<T, object>>? OrderBy { get; }
        Expression<Func<T, object>>? OrderByDescending { get; }
        int? Skip { get; }
        int? Take { get; }
        bool AsNoTracking { get; }
    }
}
