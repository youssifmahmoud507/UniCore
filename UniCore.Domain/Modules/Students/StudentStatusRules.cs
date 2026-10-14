namespace UniCore.Domain.Modules.Students
{
    public static class StudentStatusRules
    {
        private static readonly IReadOnlyDictionary<StudentAcademicStatus, StudentAcademicStatus[]> Allowed = new Dictionary<StudentAcademicStatus, StudentAcademicStatus[]>
        {
            [StudentAcademicStatus.Active] =
                [
                StudentAcademicStatus.OnLeave, StudentAcademicStatus.Suspended, StudentAcademicStatus.Withdrawn,
                StudentAcademicStatus.Dismissed, StudentAcademicStatus.Graduated
                ],
            [StudentAcademicStatus.OnLeave] =
                [
                StudentAcademicStatus.Active, StudentAcademicStatus.Withdrawn, StudentAcademicStatus.Dismissed
                ],
            [StudentAcademicStatus.Suspended] =
                [
                StudentAcademicStatus.Active, StudentAcademicStatus.Withdrawn, StudentAcademicStatus.Dismissed
                ],
            [StudentAcademicStatus.Withdrawn] = [StudentAcademicStatus.Active],
            [StudentAcademicStatus.Dismissed] = [],
            [StudentAcademicStatus.Graduated] = []
        };

        public static bool CanTransition(StudentAcademicStatus from, StudentAcademicStatus to) => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
        public static bool RequiresReason(StudentAcademicStatus to) => to is StudentAcademicStatus.OnLeave or StudentAcademicStatus.Suspended or StudentAcademicStatus.Withdrawn or StudentAcademicStatus.Dismissed;
        public static bool IsTerminal(StudentAcademicStatus status) => status is StudentAcademicStatus.Dismissed or StudentAcademicStatus.Graduated;
    }
}
