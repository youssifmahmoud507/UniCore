namespace UniCore.Domain.Modules.Graduation
{
    // The graduation thresholds. The credits come from the program (TotalRequiredCredits), so only the GPA is here.
    public sealed record GraduationPolicy(decimal MinGpa = 2.0m)
    {
        public static GraduationPolicy Default { get; } = new();
    }
}
