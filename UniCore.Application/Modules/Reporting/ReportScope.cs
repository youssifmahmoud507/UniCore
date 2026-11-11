namespace UniCore.Application.Modules.Reporting
{
    // Which parts of the university a user may see in a scoped report: everything, or the scopes below.
    public sealed record ReportScope(bool IsGlobal, IReadOnlySet<Guid> ScopeIds)
    {
        public static ReportScope Everything { get; } = new(true, new HashSet<Guid>());

        public static ReportScope Nothing { get; } = new(false, new HashSet<Guid>());

        // Data that sits in no scope at all is for global users only.
        public bool Allows(Guid? scopeId) => IsGlobal || (scopeId is { } id && ScopeIds.Contains(id));
    }




}
