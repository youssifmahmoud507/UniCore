using UniCore.Domain.Modules.Grading;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Grading
{
    public class GradingDomainTests
    {
        private static readonly DateTimeOffset Now = new(2027, 1, 22, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid Quiz = Guid.NewGuid();
        private static readonly Guid Final = Guid.NewGuid();
        private static readonly GradeResult Result = new(Guid.NewGuid(), 90m, "A-", 3.7m, true);

        private static Grade NewGrade()
            => ResultAssert.Succeeded(Grade.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, Now));

        private static Grade Submitted(Guid enteredBy)
        {
            var g = NewGrade();
            ResultAssert.Succeeded(g.SetScore(Quiz, 18, enteredBy, Now));
            ResultAssert.Succeeded(g.SetScore(Final, 90, enteredBy, Now));
            ResultAssert.Succeeded(g.Submit([Quiz, Final], Result, enteredBy, Now));

            return g;
        }

        private static Grade Approved(Guid enteredBy, Guid reviewer, Guid approver)
        {
            var g = Submitted(enteredBy);
            ResultAssert.Succeeded(g.Review(reviewer, Now));
            ResultAssert.Succeeded(g.Approve(approver, Now));

            return g;
        }

        // ---------- the scale ----------

        [Theory]
        [InlineData(100, "A+", 4.0)]
        [InlineData(97, "A+", 4.0)]
        [InlineData(96.99, "A", 4.0)]
        [InlineData(90, "A-", 3.7)]
        [InlineData(89.99, "B+", 3.3)]
        [InlineData(60, "D", 1.0)]
        [InlineData(59.99, "F", 0.0)]
        [InlineData(0, "F", 0.0)]
        public void Standard_scale_maps_a_percentage_to_a_letter_and_points(double percentage, string letter, double points)
        {
            var scale = ResultAssert.Succeeded(GradeScale.Create(GradeScale.StandardName, [.. GradeScale.StandardBands], true, Now));

            var band = ResultAssert.Succeeded(scale.Resolve((decimal)percentage));

            Assert.Equal(letter, band.Letter);
            Assert.Equal((decimal)points, band.GradePoints);
        }

        [Fact]
        public void Only_a_percentage_of_60_or_more_passes()
        {
            var scale = ResultAssert.Succeeded(GradeScale.Create(GradeScale.StandardName, [.. GradeScale.StandardBands], true, Now));

            Assert.True(ResultAssert.Succeeded(scale.Resolve(60m)).IsPassing);
            Assert.False(ResultAssert.Succeeded(scale.Resolve(59.99m)).IsPassing);
        }

        [Fact]
        public void Scale_rejects_an_impossible_percentage_and_bad_definitions()
        {
            var scale = ResultAssert.Succeeded(GradeScale.Create(GradeScale.StandardName, [.. GradeScale.StandardBands], true, Now));
            ResultAssert.Failed(scale.Resolve(-0.01m), GradingErrors.PercentageInvalid);
            ResultAssert.Failed(scale.Resolve(100.01m), GradingErrors.PercentageInvalid);

            ResultAssert.Failed(GradeScale.Create(" ", [.. GradeScale.StandardBands], true, Now), GradingErrors.ScaleInvalid);
            ResultAssert.Failed(GradeScale.Create("S", [new("A", 50m, 4m, true)], true, Now), GradingErrors.ScaleInvalid);
            ResultAssert.Failed(GradeScale.Create("S", [new("A", 50m, 4m, true), new("B", 40m, 3m, true)], true, Now), GradingErrors.ScaleInvalid);
            ResultAssert.Failed(GradeScale.Create("S", [new("A", 50m, 4m, true), new("a", 0m, 3m, true)], true, Now), GradingErrors.ScaleInvalid);
            ResultAssert.Failed(GradeScale.Create("S", [new("A", 0m, 4m, true), new("B", 0m, 3m, true)], true, Now), GradingErrors.ScaleInvalid);
        }

        // ---------- the grade ----------

        [Fact]
        public void Create_needs_every_id()
            => ResultAssert.Failed(Grade.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, Now), GradingErrors.Invalid);

        [Theory]
        [InlineData(-1)]
        [InlineData(1000.01)]
        [InlineData(10.555)]
        public void Score_must_be_between_0_and_1000_with_two_decimals(double score)
            => ResultAssert.Failed(NewGrade().SetScore(Quiz, (decimal)score, null, Now), GradingErrors.ScoreInvalid);

        [Fact]
        public void A_new_score_replaces_the_old_one_for_the_same_component()
        {
            var g = NewGrade();
            ResultAssert.Succeeded(g.SetScore(Quiz, 10, null, Now));
            ResultAssert.Succeeded(g.SetScore(Quiz, 15, null, Now));

            Assert.Equal(15, g.Scores.Single().Score);
        }

        [Fact]
        public void Submit_needs_a_score_for_every_component()
        {
            var g = NewGrade();
            ResultAssert.Succeeded(g.SetScore(Quiz, 10, null, Now));

            ResultAssert.Failed(g.Submit([Quiz, Final], Result, null, Now), GradingErrors.ScoresIncomplete);
            ResultAssert.Failed(g.Submit([], Result, null, Now), GradingErrors.ScoresIncomplete);
        }

        [Fact]
        public void Submit_keeps_the_result_and_locks_the_scores()
        {
            var g = Submitted(Guid.NewGuid());

            Assert.Equal(GradeStatus.Submitted, g.Status);
            Assert.Equal("A-", g.Letter);
            Assert.Equal(3.7m, g.GradePoints);
            ResultAssert.Failed(g.SetScore(Quiz, 1, null, Now), GradingErrors.InvalidState);
            ResultAssert.Failed(g.Submit([Quiz], Result, null, Now), GradingErrors.InvalidState);
        }

        [Fact]
        public void The_person_who_entered_a_grade_cannot_review_it()
        {
            var author = Guid.NewGuid();
            var g = Submitted(author);

            ResultAssert.Failed(g.Review(author, Now), GradingErrors.SeparationOfDuties);
            ResultAssert.Failed(g.Review(Guid.Empty, Now), GradingErrors.SeparationOfDuties);
            ResultAssert.Succeeded(g.Review(Guid.NewGuid(), Now));
        }

        [Fact]
        public void The_author_and_the_reviewer_cannot_approve()
        {
            var author = Guid.NewGuid();
            var reviewer = Guid.NewGuid();
            var g = Submitted(author);
            ResultAssert.Succeeded(g.Review(reviewer, Now));

            ResultAssert.Failed(g.Approve(author, Now), GradingErrors.SeparationOfDuties);
            ResultAssert.Failed(g.Approve(reviewer, Now), GradingErrors.SeparationOfDuties);
            ResultAssert.Succeeded(g.Approve(Guid.NewGuid(), Now));
        }

        [Fact]
        public void Steps_must_follow_the_order()
        {
            var g = Submitted(Guid.NewGuid());

            ResultAssert.Failed(g.Approve(Guid.NewGuid(), Now), GradingErrors.InvalidState);
            ResultAssert.Failed(g.Publish(Now), GradingErrors.InvalidState);
            ResultAssert.Failed(g.Lock(Now), GradingErrors.InvalidState);
        }

        [Fact]
        public void Return_needs_a_reason_and_makes_the_grade_editable_again()
        {
            var g = Submitted(Guid.NewGuid());

            ResultAssert.Failed(g.ReturnToDraft(" ", Now), GradingErrors.ReasonRequired);
            ResultAssert.Failed(g.ReturnToDraft(new string('x', 501), Now), GradingErrors.ReasonRequired);

            ResultAssert.Succeeded(g.ReturnToDraft("Midterm mark is wrong", Now));
            Assert.Equal(GradeStatus.Draft, g.Status);
            Assert.Equal("Midterm mark is wrong", g.ReturnReason);
            ResultAssert.Succeeded(g.SetScore(Quiz, 19, null, Now));
        }

        [Fact]
        public void A_draft_or_approved_grade_cannot_be_returned()
        {
            ResultAssert.Failed(NewGrade().ReturnToDraft("x", Now), GradingErrors.InvalidState);
            ResultAssert.Failed(Approved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).ReturnToDraft("x", Now), GradingErrors.InvalidState);
        }

        [Fact]
        public void Publish_raises_an_event_with_the_snapshot_for_gpa()
        {
            var g = Approved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            ResultAssert.Succeeded(g.Publish(Now));

            Assert.Equal(GradeStatus.Published, g.Status);
            var published = Assert.IsType<GradePublished>(Assert.Single(g.DomainEvents));
            Assert.Equal(3, published.Credits);
            Assert.Equal("A-", published.Letter);
            Assert.Equal(3.7m, published.GradePoints);
            Assert.True(published.IsPassing);
        }

        [Fact]
        public void A_published_or_locked_grade_is_protected()
        {
            var g = Approved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            ResultAssert.Succeeded(g.Publish(Now));

            ResultAssert.Failed(g.SetScore(Quiz, 1, null, Now), GradingErrors.Locked);
            ResultAssert.Failed(g.ReturnToDraft("x", Now), GradingErrors.Locked);
            ResultAssert.Failed(g.Publish(Now), GradingErrors.Locked);

            ResultAssert.Succeeded(g.Lock(Now));
            ResultAssert.Failed(g.SetScore(Quiz, 1, null, Now), GradingErrors.Locked);
            ResultAssert.Failed(g.Lock(Now), GradingErrors.Locked);
            Assert.Equal(GradeStatus.Locked, g.Status);
        }

        [Fact]
        public void Correct_changes_only_a_final_grade_and_raises_an_event()
        {
            var draft = NewGrade();
            ResultAssert.Failed(draft.Correct(Quiz, 5, Result, "x", Now), GradingErrors.InvalidState);

            var g = Approved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            ResultAssert.Succeeded(g.Publish(Now));

            ResultAssert.Failed(g.Correct(Guid.NewGuid(), 5, Result, "x", Now), GradingErrors.CorrectionInvalid);
            ResultAssert.Failed(g.Correct(Quiz, -1, Result, "x", Now), GradingErrors.CorrectionInvalid);
            ResultAssert.Failed(g.Correct(Quiz, 5, Result, " ", Now), GradingErrors.CorrectionInvalid);

            var better = new GradeResult(Result.GradeScaleId, 97m, "A+", 4.0m, true);
            ResultAssert.Succeeded(g.Correct(Quiz, 20, better, "Quiz mark was mis-typed", Now));

            Assert.Equal(GradeStatus.Published, g.Status);
            Assert.Equal("A+", g.Letter);
            Assert.Equal(20, g.Scores.Single(s => s.AssessmentComponentId == Quiz).Score);
            var corrected = g.DomainEvents.OfType<GradeCorrected>().Single();
            Assert.Equal("A-", corrected.OldLetter);
            Assert.Equal("A+", corrected.NewLetter);
        }
    }
}
