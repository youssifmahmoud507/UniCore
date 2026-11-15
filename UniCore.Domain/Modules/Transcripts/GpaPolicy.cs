using UniCore.Domain.Modules.Students;

namespace UniCore.Domain.Modules.Transcripts
{
    // The rules of the GPA. Today there is one default policy; the calculator takes it as a parameter so a faculty-specific one can come later.
    public sealed record GpaPolicy(
        RepeatPolicy Repeat = RepeatPolicy.LatestAttempt,
        decimal HonorsMinGpa = 3.5m,
        int HonorsMinCredits = 12,
        decimal GoodStandingMinGpa = 2.0m,
        decimal WarningMinGpa = 1.5m)
    {
        public static GpaPolicy Default { get; } = new();

        // Honors: GPA of 3.5 or more over at least 12 counted credits. GoodStanding: 2.0 or more. Warning: 1.5 up to 2.0. Probation: below 1.5.
        public AcademicStanding StandingOf(decimal gpa, int attemptedCredits)
            => gpa >= HonorsMinGpa && attemptedCredits >= HonorsMinCredits ? AcademicStanding.Honors
             : gpa >= GoodStandingMinGpa ? AcademicStanding.GoodStanding
             : gpa >= WarningMinGpa ? AcademicStanding.Warning
             : AcademicStanding.Probation;
    }
}
