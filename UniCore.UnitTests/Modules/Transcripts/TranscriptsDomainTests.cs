using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Transcripts;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Transcripts
{
    public class TranscriptsDomainTests
    {
        private static readonly DateTimeOffset Now = new(2027, 2, 3, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid Fall = Guid.NewGuid();
        private static readonly Guid Spring = Guid.NewGuid();
        private static readonly DateOnly FallStart = new(2026, 9, 1);
        private static readonly DateOnly SpringStart = new(2027, 2, 15);

        private static GradeAttempt Attempt(Guid course, Guid semester, DateOnly start, int credits, decimal points, bool passing = true, int publishedDay = 1)
            => new(Guid.NewGuid(), course, semester, start, credits, passing ? 80m : 40m, passing ? "B" : "F", points, passing, Now.AddDays(publishedDay));

        // ---------- the GPA ----------

        [Fact]
        public void Gpa_is_quality_points_over_credits_rounded_to_two_decimals()
        {
            var c1 = Guid.NewGuid();
            var c2 = Guid.NewGuid();
            var summary = GpaCalculator.Summarize([Attempt(c1, Fall, FallStart, 3, 4.0m), Attempt(c2, Fall, FallStart, 4, 3.0m)], GpaPolicy.Default);

            // (12 + 12) / 7 = 3.428... -> 3.43
            Assert.Equal(3.43m, summary.Gpa);
            Assert.Equal(7, summary.AttemptedCredits);
            Assert.Equal(7, summary.EarnedCredits);
        }

        [Fact]
        public void Failed_credits_are_attempted_but_not_earned()
        {
            var summary = GpaCalculator.Summarize([Attempt(Guid.NewGuid(), Fall, FallStart, 3, 4.0m), Attempt(Guid.NewGuid(), Fall, FallStart, 3, 0m, passing: false)], GpaPolicy.Default);

            Assert.Equal(2.00m, summary.Gpa);
            Assert.Equal(6, summary.AttemptedCredits);
            Assert.Equal(3, summary.EarnedCredits);
        }

        [Fact]
        public void No_grades_means_zero_gpa_and_no_standing()
        {
            var summary = GpaCalculator.Summarize([], GpaPolicy.Default);

            Assert.Equal(0m, summary.Gpa);
            Assert.Equal(0, summary.AttemptedCredits);
            Assert.Null(summary.Standing);
        }

        // ---------- the repeat policy ----------

        [Fact]
        public void Latest_attempt_policy_counts_only_the_most_recent_attempt_of_a_course()
        {
            var course = Guid.NewGuid();
            var first = Attempt(course, Fall, FallStart, 3, 3.0m);
            var second = Attempt(course, Spring, SpringStart, 3, 2.0m);

            var outcomes = GpaCalculator.Resolve([first, second], new GpaPolicy(RepeatPolicy.LatestAttempt));

            Assert.False(outcomes.Single(o => o.Attempt == first).IsCounted);
            Assert.True(outcomes.Single(o => o.Attempt == second).IsCounted);
            Assert.Equal(2.00m, GpaCalculator.Summarize([first, second], GpaPolicy.Default).Gpa);
        }

        [Fact]
        public void Highest_grade_policy_counts_the_best_attempt_even_when_it_is_the_older_one()
        {
            var course = Guid.NewGuid();
            var first = Attempt(course, Fall, FallStart, 3, 3.0m);
            var second = Attempt(course, Spring, SpringStart, 3, 2.0m);
            var policy = new GpaPolicy(RepeatPolicy.HighestGrade);

            var summary = GpaCalculator.Summarize([first, second], policy);

            Assert.Equal(3.00m, summary.Gpa);
            Assert.Equal(3, summary.AttemptedCredits);
            Assert.Equal(1, summary.ReplacedAttempts);
        }

        [Fact]
        public void A_repeated_course_is_counted_once_not_twice()
        {
            var course = Guid.NewGuid();
            var summary = GpaCalculator.Summarize([Attempt(course, Fall, FallStart, 3, 0m, passing: false), Attempt(course, Spring, SpringStart, 3, 4.0m)], GpaPolicy.Default);

            Assert.Equal(3, summary.AttemptedCredits);
            Assert.Equal(3, summary.EarnedCredits);
            Assert.Equal(4.00m, summary.Gpa);
        }

        // ---------- semester vs cumulative ----------

        [Fact]
        public void Each_semester_has_its_own_gpa_and_the_cumulative_gpa_at_its_end()
        {
            var semesters = GpaCalculator.BySemester([
                Attempt(Guid.NewGuid(), Fall, FallStart, 3, 4.0m),
                Attempt(Guid.NewGuid(), Spring, SpringStart, 3, 2.0m)], GpaPolicy.Default);

            Assert.Equal(2, semesters.Count);
            Assert.Equal(Fall, semesters[0].SemesterId);
            Assert.Equal(4.00m, semesters[0].Semester.Gpa);
            Assert.Equal(4.00m, semesters[0].Cumulative.Gpa);
            Assert.Equal(2.00m, semesters[1].Semester.Gpa);
            Assert.Equal(3.00m, semesters[1].Cumulative.Gpa);
        }

        [Fact]
        public void A_replaced_attempt_shows_as_not_counted_in_its_own_semester()
        {
            var course = Guid.NewGuid();
            var semesters = GpaCalculator.BySemester([Attempt(course, Fall, FallStart, 3, 1.0m), Attempt(course, Spring, SpringStart, 3, 3.0m)], GpaPolicy.Default);

            Assert.False(semesters[0].Outcomes.Single().IsCounted);
            Assert.True(semesters[1].Outcomes.Single().IsCounted);
        }

        // ---------- academic standing ----------

        [Theory]
        [InlineData(3.50, 12, AcademicStanding.Honors)]
        [InlineData(3.49, 12, AcademicStanding.GoodStanding)]
        [InlineData(3.90, 9, AcademicStanding.GoodStanding)]
        [InlineData(2.00, 30, AcademicStanding.GoodStanding)]
        [InlineData(1.99, 30, AcademicStanding.Warning)]
        [InlineData(1.50, 30, AcademicStanding.Warning)]
        [InlineData(1.49, 30, AcademicStanding.Probation)]
        public void Standing_follows_the_policy_thresholds(double gpa, int credits, AcademicStanding expected)
            => Assert.Equal(expected, GpaPolicy.Default.StandingOf((decimal)gpa, credits));

        // ---------- the official snapshot ----------

        private static TranscriptEntryData Entry(string code = "CS101")
            => new(Guid.NewGuid(), Fall, "2026/2027 - Fall", FallStart, Guid.NewGuid(), code, "Intro", 3, 90m, "A-", 3.7m, true, true, 3.7m, 3.7m);

        private static GpaSummary Summary() => new(3.70m, 3, 3, 11.1m, 1, 0, AcademicStanding.Honors);

        [Fact]
        public void Issue_freezes_the_figures_orders_the_entries_and_raises_the_event()
        {
            var snapshot = ResultAssert.Succeeded(TranscriptSnapshot.Issue(Guid.NewGuid(), "2026CS1", "abcdef123456", Guid.NewGuid(), GpaPolicy.Default, Summary(), [Entry("CS102"), Entry("CS101")], Now));

            Assert.Equal("ABCDEF123456", snapshot.VerificationCode);
            Assert.Equal(3.70m, snapshot.CumulativeGpa);
            Assert.Equal(["CS101", "CS102"], snapshot.Entries.Select(e => e.CourseCode));
            Assert.Equal([1, 2], snapshot.Entries.Select(e => e.SequenceNo));
            Assert.Single(snapshot.DomainEvents.OfType<TranscriptIssued>());
        }

        [Fact]
        public void Issue_rejects_missing_data()
        {
            var user = Guid.NewGuid();

            ResultAssert.Failed(TranscriptSnapshot.Issue(Guid.NewGuid(), "2026CS1", "abcdef123456", user, GpaPolicy.Default, Summary(), [], Now), TranscriptErrors.SnapshotInvalid);
            ResultAssert.Failed(TranscriptSnapshot.Issue(Guid.NewGuid(), "2026CS1", "short", user, GpaPolicy.Default, Summary(), [Entry()], Now), TranscriptErrors.SnapshotInvalid);
            ResultAssert.Failed(TranscriptSnapshot.Issue(Guid.NewGuid(), " ", "abcdef123456", user, GpaPolicy.Default, Summary(), [Entry()], Now), TranscriptErrors.SnapshotInvalid);
            ResultAssert.Failed(TranscriptSnapshot.Issue(Guid.Empty, "2026CS1", "abcdef123456", user, GpaPolicy.Default, Summary(), [Entry()], Now), TranscriptErrors.SnapshotInvalid);
            ResultAssert.Failed(TranscriptSnapshot.Issue(Guid.NewGuid(), "2026CS1", "abcdef123456", Guid.Empty, GpaPolicy.Default, Summary(), [Entry()], Now), TranscriptErrors.SnapshotInvalid);
        }
    }
}
