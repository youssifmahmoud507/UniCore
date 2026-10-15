namespace UniCore.Application.Common.Sorting
{
    // One validated sort: a field name from the caller's whitelist and a direction.
    public sealed record SortOption(string Field, bool Descending);
}
