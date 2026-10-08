using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Common.Pagination
{
    public sealed record PaginationQuery(int Page = 1, int PageSize = PaginationQuery.DefaultPageSize)
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
        public int NormalizedPage => Page < 1 ? 1 : Page;
        public int NormalizedPageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);
        public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
    }
}
