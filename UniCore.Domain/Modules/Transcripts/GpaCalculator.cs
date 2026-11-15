using UniCore.Domain.Modules.Students;

namespace UniCore.Domain.Modules.Transcripts
{
    // One published grade of a student, as GPA sees it (from IGradeReadService plus the semester's start date).
    public sealed record GradeAttempt(
        Guid GradeId, Guid CourseId, Guid SemesterId, DateOnly SemesterStart, int Credits,
        decimal Percentage, string Letter, decimal GradePoints, bool IsPassing, DateTimeOffset PublishedAt);

    // An attempt and whether it counts (false = replaced by another attempt of the same course under the repeat policy).
    public sealed record AttemptOutcome(GradeAttempt Attempt, bool IsCounted);

    // Gpa = quality points / attempted credits, rounded to 2 decimals (0 when there are no credits; Standing is then null).
    // AttemptedCredits are the credits of the COUNTED attempts; EarnedCredits those of the counted attempts that passed.
    public sealed record GpaSummary(decimal Gpa, int AttemptedCredits, int EarnedCredits, decimal QualityPoints, int CountedAttempts, int ReplacedAttempts, AcademicStanding? Standing);

    // One semester: its own GPA and the cumulative GPA as it stood at the end of it (counting only what happened up to then).
    public sealed record SemesterGpa(Guid SemesterId, DateOnly SemesterStart, GpaSummary Semester, GpaSummary Cumulative, IReadOnlyList<AttemptOutcome> Outcomes);

    // Pure functions: no I/O, no clock. Everything the transcript shows is calculated here.
    public static class GpaCalculator
    {
        // Applies the repeat policy: exactly one attempt per course is counted.
        public static IReadOnlyList<AttemptOutcome> Resolve(IEnumerable<GradeAttempt> attempts, GpaPolicy policy)
        {
            var outcomes = new List<AttemptOutcome>();

            foreach (var course in attempts.GroupBy(a => a.CourseId))
            {
                var counted = policy.Repeat == RepeatPolicy.HighestGrade
                    ? course.OrderBy(a => a.GradePoints).ThenBy(a => a.SemesterStart).ThenBy(a => a.PublishedAt).Last()
                    : course.OrderBy(a => a.SemesterStart).ThenBy(a => a.PublishedAt).Last();

                outcomes.AddRange(course.Select(a => new AttemptOutcome(a, ReferenceEquals(a, counted))));
            }

            return outcomes;
        }

        // The cumulative figures of a set of attempts, repeat policy applied.
        public static GpaSummary Summarize(IEnumerable<GradeAttempt> attempts, GpaPolicy policy)
            => SummarizeOutcomes(Resolve(attempts, policy));

        // The semesters in date order, each with its own GPA and the cumulative GPA at its end. The outcomes carry the FINAL
        // counted flag (a course repeated later shows its earlier attempt as not counted).
        public static IReadOnlyList<SemesterGpa> BySemester(IReadOnlyCollection<GradeAttempt> attempts, GpaPolicy policy)
        {
            var final = Resolve(attempts, policy);
            var result = new List<SemesterGpa>();

            foreach (var semester in attempts.GroupBy(a => new { a.SemesterId, a.SemesterStart }).OrderBy(g => g.Key.SemesterStart))
            {
                var outcomes = final.Where(o => o.Attempt.SemesterId == semester.Key.SemesterId).ToList();
                var upToHere = attempts.Where(a => a.SemesterStart <= semester.Key.SemesterStart);

                result.Add(new SemesterGpa(semester.Key.SemesterId, semester.Key.SemesterStart, SummarizeOutcomes(outcomes, policy), Summarize(upToHere, policy), outcomes));
            }

            return result;
        }

        private static GpaSummary SummarizeOutcomes(IReadOnlyList<AttemptOutcome> outcomes, GpaPolicy? policy = null)
        {
            var effective = policy ?? GpaPolicy.Default;
            var counted = outcomes.Where(o => o.IsCounted && o.Attempt.Credits > 0).Select(o => o.Attempt).ToList();

            var attempted = counted.Sum(a => a.Credits);
            var earned = counted.Where(a => a.IsPassing).Sum(a => a.Credits);
            var quality = counted.Sum(a => a.GradePoints * a.Credits);
            var gpa = attempted == 0 ? 0m : decimal.Round(quality / attempted, 2, MidpointRounding.AwayFromZero);

            return new GpaSummary(gpa, attempted, earned, quality, counted.Count, outcomes.Count(o => !o.IsCounted), attempted == 0 ? null : effective.StandingOf(gpa, attempted));
        }
    }
}
