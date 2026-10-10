namespace UniCore.Domain.Modules.Identity
{
    public static class RoleNames
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Dean = "Dean";
        public const string DepartmentHead = "DepartmentHead";
        public const string Instructor = "Instructor";
        public const string Student = "Student";
        public const string Registrar = "Registrar";
        public const string FinanceOfficer = "FinanceOfficer";
        public const string AdmissionsOfficer = "AdmissionsOfficer";

        public static readonly IReadOnlyList<string> All =
        [
        SuperAdmin, Dean, DepartmentHead, Instructor,
        Student, Registrar, FinanceOfficer, AdmissionsOfficer
    ];
    }
}
