namespace UniCore.Domain.Modules.Identity
{
    // Permission codes: Module.Action. Add a code here and the seeder creates it on the next start.
    public static class Permissions
    {
        public static class Users
        {
            public const string Read = "Users.Read";
            public const string Manage = "Users.Manage";
        }

        public static class Roles
        {
            public const string Manage = "Roles.Manage";
        }

        // Named PermissionAdmin because a nested type cannot share the name of its enclosing type.
        public static class PermissionAdmin
        {
            public const string Manage = "Permissions.Manage";
        }

        public static class University
        {
            public const string Read = "University.Read";
            public const string Manage = "University.Manage";
        }

        public static class People
        {
            public const string Read = "People.Read";
            public const string Manage = "People.Manage";
        }

        public static class Students
        {
            public const string Read = "Students.Read";
            public const string Manage = "Students.Manage";
            public const string ChangeStatus = "Students.ChangeStatus";
            public const string ChangeProgram = "Students.ChangeProgram";
        }

        public static class Admissions
        {
            public const string Read = "Admissions.Read";
            public const string Review = "Admissions.Review";
            public const string Decide = "Admissions.Decide";
        }

        public static class Courses
        {
            public const string Read = "Courses.Read";
            public const string Manage = "Courses.Manage";
        }

        public static class Curriculum
        {
            public const string Manage = "Curriculum.Manage";
        }

        public static class Registration
        {
            public const string Read = "Registration.Read";
            public const string Register = "Registration.Register";
            public const string Manage = "Registration.Manage";
        }

        public static class Attendance
        {
            public const string Read = "Attendance.Read";
            public const string Record = "Attendance.Record";
            public const string ReviewExcuse = "Attendance.ReviewExcuse";
        }

        public static class Exams
        {
            public const string Read = "Exams.Read";
            public const string Manage = "Exams.Manage";
        }

        public static class Grades
        {
            public const string Read = "Grades.Read";
            public const string Enter = "Grades.Enter";
            public const string Review = "Grades.Review";
            public const string Approve = "Grades.Approve";
            public const string Publish = "Grades.Publish";
        }

        public static class GradeChanges
        {
            public const string Request = "GradeChanges.Request";
            public const string Approve = "GradeChanges.Approve";
        }

        public static class Finance
        {
            public const string Read = "Finance.Read";
        }

        public static class Fees
        {
            public const string Manage = "Fees.Manage";
        }

        public static class Invoices
        {
            public const string Manage = "Invoices.Manage";
        }

        public static class Payments
        {
            public const string Record = "Payments.Record";
        }

        public static class Refunds
        {
            public const string Approve = "Refunds.Approve";
        }

        public static class Holds
        {
            public const string Manage = "Holds.Manage";
        }

        public static class Requests
        {
            public const string Submit = "Requests.Submit";
            public const string Read = "Requests.Read";
            public const string Process = "Requests.Process";
        }

        public static class Documents
        {
            public const string Read = "Documents.Read";
            public const string Upload = "Documents.Upload";
            public const string Manage = "Documents.Manage";
        }

        public static class Audit
        {
            public const string Read = "Audit.Read";
        }

        public static class Settings
        {
            public const string Manage = "Settings.Manage";
        }

        public static class Reports
        {
            public const string Read = "Reports.Read";
        }

        public static readonly IReadOnlyList<PermissionDefinition> All =
        [
        new PermissionDefinition(Users.Read, "Identity", "View user accounts"),
        new PermissionDefinition(Users.Manage, "Identity", "Create, update, activate and deactivate user accounts"),
        new PermissionDefinition(Roles.Manage, "Identity", "Manage roles, role assignments and scopes"),
        new PermissionDefinition(PermissionAdmin.Manage, "Identity", "Manage the permissions of roles"),
        new PermissionDefinition(University.Read, "University", "View the university structure"),
        new PermissionDefinition(University.Manage, "University", "Manage the university structure"),
        new PermissionDefinition(People.Read, "People", "View people, employees and instructors"),
        new PermissionDefinition(People.Manage, "People", "Manage people, employees and instructors"),
        new PermissionDefinition(Students.Read, "Students", "View students"),
        new PermissionDefinition(Students.Manage, "Students", "Manage students"),
        new PermissionDefinition(Students.ChangeStatus, "Students", "Change a student's academic status"),
        new PermissionDefinition(Students.ChangeProgram, "Students", "Change a student's program"),
        new PermissionDefinition(Admissions.Read, "Admissions", "View admission applications"),
        new PermissionDefinition(Admissions.Review, "Admissions", "Review admission applications"),
        new PermissionDefinition(Admissions.Decide, "Admissions", "Decide admission applications"),
        new PermissionDefinition(Courses.Read, "Courses", "View courses, offerings and sections"),
        new PermissionDefinition(Courses.Manage, "Courses", "Manage courses, offerings and sections"),
        new PermissionDefinition(Curriculum.Manage, "Curriculum", "Manage study plans and prerequisites"),
        new PermissionDefinition(Registration.Read, "Registration", "View registrations and enrollments"),
        new PermissionDefinition(Registration.Register, "Registration", "Register in courses"),
        new PermissionDefinition(Registration.Manage, "Registration", "Manage registrations on behalf of students"),
        new PermissionDefinition(Attendance.Read, "Attendance", "View attendance"),
        new PermissionDefinition(Attendance.Record, "Attendance", "Record attendance"),
        new PermissionDefinition(Attendance.ReviewExcuse, "Attendance", "Review attendance excuses"),
        new PermissionDefinition(Exams.Read, "Examination", "View exams and schedules"),
        new PermissionDefinition(Exams.Manage, "Examination", "Manage exams and schedules"),
        new PermissionDefinition(Grades.Read, "Grading", "View grades"),
        new PermissionDefinition(Grades.Enter, "Grading", "Enter grades"),
        new PermissionDefinition(Grades.Review, "Grading", "Review submitted grades"),
        new PermissionDefinition(Grades.Approve, "Grading", "Approve reviewed grades"),
        new PermissionDefinition(Grades.Publish, "Grading", "Publish approved grades"),
        new PermissionDefinition(GradeChanges.Request, "Grading", "Request a change of a locked grade"),
        new PermissionDefinition(GradeChanges.Approve, "Grading", "Approve a grade change request"),
        new PermissionDefinition(Finance.Read, "Finance", "View balances, invoices and payments"),
        new PermissionDefinition(Invoices.Manage, "Finance", "Manage invoices and charges"),
        new PermissionDefinition(Payments.Record, "Finance", "Record payments"),
        new PermissionDefinition(Refunds.Approve, "Finance", "Approve refunds"),
        new PermissionDefinition(Holds.Manage, "Finance", "Place and release financial holds"),
        new PermissionDefinition(Requests.Submit, "Services", "Submit student service requests"),
        new PermissionDefinition(Requests.Read, "Services", "View student service requests"),
        new PermissionDefinition(Requests.Process, "Services", "Process student service requests"),
        new PermissionDefinition(Documents.Read, "Documents", "View documents"),
        new PermissionDefinition(Documents.Upload, "Documents", "Upload documents"),
        new PermissionDefinition(Documents.Manage, "Documents", "Manage documents and access"),
        new PermissionDefinition(Audit.Read, "Audit", "View the audit log"),
        new PermissionDefinition(Settings.Manage, "Platform", "Manage system settings"),
        new PermissionDefinition(Reports.Read, "Platform", "View reports"),
        new PermissionDefinition(Fees.Manage, "Finance", "Manage fee definitions and fee structures"),
    ];

        // Declared after All: static initializers run in the order they are written.
        public static readonly IReadOnlySet<string> AllCodes = All.Select(d => d.Code).ToHashSet(StringComparer.Ordinal);
    }
}
