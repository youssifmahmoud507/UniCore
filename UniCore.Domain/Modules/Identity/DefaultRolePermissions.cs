namespace UniCore.Domain.Modules.Identity
{
    // Proposed starting permissions, seeded once per role (only while the role has no permissions).
    // SuperAdmin is not listed: it implicitly holds every permission.
    public static class DefaultRolePermissions
    {
        private static readonly string[] StudentSet =
        [
        Permissions.Registration.Read, Permissions.Registration.Register, Permissions.Grades.Read,
        Permissions.Finance.Read, Permissions.Requests.Submit, Permissions.Requests.Read,
        Permissions.Documents.Read, Permissions.Documents.Upload
    ];

        private static readonly string[] InstructorSet =
        [
        Permissions.Courses.Read, Permissions.Attendance.Read, Permissions.Attendance.Record,
        Permissions.Exams.Read, Permissions.Grades.Read, Permissions.Grades.Enter,
        Permissions.Documents.Read, Permissions.Documents.Upload
    ];

        private static readonly string[] DepartmentHeadSet =
        [
        Permissions.University.Read, Permissions.People.Read, Permissions.Students.Read,
        Permissions.Courses.Read, Permissions.Courses.Manage, Permissions.Registration.Read,
        Permissions.Attendance.Read, Permissions.Attendance.ReviewExcuse,
        Permissions.Exams.Read, Permissions.Exams.Manage,
        Permissions.Grades.Read, Permissions.Grades.Review, Permissions.GradeChanges.Request,
        Permissions.Requests.Read, Permissions.Requests.Process,
        Permissions.Documents.Read, Permissions.Reports.Read
    ];

        private static readonly string[] DeanSet =
        [
            .. DepartmentHeadSet
,
            .. new[]
                {
                Permissions.Curriculum.Manage, Permissions.Admissions.Read,
                Permissions.Grades.Approve, Permissions.GradeChanges.Approve
                },
        ];

        private static readonly string[] RegistrarSet =
        [
        Permissions.University.Read, Permissions.People.Read, Permissions.People.Manage,
        Permissions.Students.Read, Permissions.Students.Manage,
        Permissions.Students.ChangeStatus, Permissions.Students.ChangeProgram,
        Permissions.Admissions.Read, Permissions.Courses.Read, Permissions.Courses.Manage,
        Permissions.Curriculum.Manage, Permissions.Registration.Read, Permissions.Registration.Manage,
        Permissions.Grades.Read, Permissions.Grades.Publish,
        Permissions.Requests.Read, Permissions.Requests.Process,
        Permissions.Documents.Read, Permissions.Documents.Manage, Permissions.Reports.Read
    ];

        private static readonly string[] FinanceOfficerSet =
        [
        Permissions.Students.Read, Permissions.Finance.Read, Permissions.Invoices.Manage,
        Permissions.Payments.Record, Permissions.Refunds.Approve, Permissions.Holds.Manage,
        Permissions.Documents.Read, Permissions.Reports.Read
    ];

        private static readonly string[] AdmissionsOfficerSet =
        [
        Permissions.People.Read, Permissions.Admissions.Read, Permissions.Admissions.Review,
        Permissions.Admissions.Decide, Permissions.Documents.Read
    ];

        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [RoleNames.Student] = StudentSet,
                [RoleNames.Instructor] = InstructorSet,
                [RoleNames.DepartmentHead] = DepartmentHeadSet,
                [RoleNames.Dean] = DeanSet,
                [RoleNames.Registrar] = RegistrarSet,
                [RoleNames.FinanceOfficer] = FinanceOfficerSet,
                [RoleNames.AdmissionsOfficer] = AdmissionsOfficerSet
            };
    }


}
