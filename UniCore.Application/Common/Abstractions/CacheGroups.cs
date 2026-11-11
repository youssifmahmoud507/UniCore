namespace UniCore.Application.Common.Abstractions
{
    public static class CacheGroups
    {
        // Roles, permissions and scopes of a user. Cleared whenever a role, a permission, a scope, a role assignment
        // or the active flag of a user changes.
        public const string UserAccess = "user-access";

        // The list of document types. Cleared whenever a document type changes.
        public const string DocumentTypes = "document-types";

        // The numbers of the dashboard widgets. Not cleared by changes: they simply expire after a minute.
        public const string Dashboard = "dashboard";
    }
}


