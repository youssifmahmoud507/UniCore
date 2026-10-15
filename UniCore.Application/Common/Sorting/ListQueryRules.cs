using UniCore.Domain.Common.Results;

namespace UniCore.Application.Common.Sorting
{
    // Shared rules for list endpoints: the sort whitelist (conventions.md section 4) and the search term.
    public static class ListQueryRules
    {
        public const int MaxSearchLength = 50;

        public static readonly Error SortFieldInvalid = Error.Validation("SORT_FIELD_INVALID", "The sort field is not allowed.");
        public static readonly Error SortMultipleNotSupported = Error.Validation("SORT_MULTIPLE_NOT_SUPPORTED", "Only one sort field is supported for now.");

        // "name" or "-name". Null or blank gives the default field, ascending.
        // Only a field in `allowed` is accepted; the comparison ignores case and the returned field uses the
        // spelling from `allowed`, so callers can switch on it safely.
        public static Result<SortOption> ParseSort(string? sort, IReadOnlyCollection<string> allowed, string defaultField)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return new SortOption(defaultField, false);
            }

            var text = sort.Trim();

            if (text.Contains(','))
            {
                return SortMultipleNotSupported;
            }

            var descending = text.StartsWith('-');
            var name = descending ? text[1..] : text;

            foreach (var candidate in allowed)
            {
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new SortOption(candidate, descending);
                }
            }

            return SortFieldInvalid;
        }

        // Blank becomes null (no filter); long text is cut so a huge string never reaches the database.
        public static string? NormalizeSearch(string? search)
        {
            var term = search?.Trim();

            if (string.IsNullOrEmpty(term))
            {
                return null;
            }

            return term.Length > MaxSearchLength ? term[..MaxSearchLength] : term;
        }
    }
}
