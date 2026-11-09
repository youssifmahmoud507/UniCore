using UniCore.Domain.Modules.Assessments;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Assessments
{
    public class AssessmentDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Due = new(2026, 10, 15);

        private static Assessment New() => ResultAssert.Succeeded(Assessment.Create(Guid.NewGuid(), Now));

        private static Assessment Full()
        {
            var a = New();
            ResultAssert.Succeeded(a.AddComponent("Quizzes", ComponentType.Quiz, 20, 20, Due, Now));
            ResultAssert.Succeeded(a.AddComponent("Midterm", ComponentType.Midterm, 30, 50, Due, Now));
            ResultAssert.Succeeded(a.AddComponent("Final", ComponentType.Final, 50, 100, Due, Now));

            return a;
        }

        [Fact]
        public void Create_needs_a_section_and_starts_as_draft()
        {
            ResultAssert.Failed(Assessment.Create(Guid.Empty, Now), AssessmentErrors.Invalid);
            Assert.Equal(AssessmentStatus.Draft, New().Status);
        }

        [Theory]
        [InlineData(null, 10, 20)]
        [InlineData(" ", 10, 20)]
        [InlineData("Quiz", 0, 20)]
        [InlineData("Quiz", -5, 20)]
        [InlineData("Quiz", 101, 20)]
        [InlineData("Quiz", 10.555, 20)]
        [InlineData("Quiz", 10, 0)]
        [InlineData("Quiz", 10, 1000.5)]
        public void Component_rejects_invalid_values(string? name, double weight, double max)
            => ResultAssert.Failed(New().AddComponent(name, ComponentType.Quiz, (decimal)weight, (decimal)max, Due, Now), AssessmentErrors.ComponentInvalid);

        [Fact]
        public void Component_rejects_a_long_name_an_unknown_type_and_a_default_date()
        {
            var a = New();

            ResultAssert.Failed(a.AddComponent(new string('x', 101), ComponentType.Quiz, 10, 20, Due, Now), AssessmentErrors.ComponentInvalid);
            ResultAssert.Failed(a.AddComponent("Q", (ComponentType)99, 10, 20, Due, Now), AssessmentErrors.ComponentInvalid);
            ResultAssert.Failed(a.AddComponent("Q", ComponentType.Quiz, 10, 20, default(DateOnly), Now), AssessmentErrors.ComponentInvalid);
        }

        [Fact]
        public void Names_are_unique_ignoring_case_and_spaces()
        {
            var a = New();
            ResultAssert.Succeeded(a.AddComponent("Quiz 1", ComponentType.Quiz, 10, 20, Due, Now));

            ResultAssert.Failed(a.AddComponent(" quiz 1 ", ComponentType.Quiz, 10, 20, Due, Now), AssessmentErrors.ComponentNameExists);
        }

        [Fact]
        public void There_is_one_midterm_and_one_final_at_most()
        {
            var a = New();
            ResultAssert.Succeeded(a.AddComponent("Midterm", ComponentType.Midterm, 20, 50, Due, Now));
            ResultAssert.Succeeded(a.AddComponent("Final", ComponentType.Final, 40, 100, Due, Now));

            ResultAssert.Failed(a.AddComponent("Midterm 2", ComponentType.Midterm, 10, 50, Due, Now), AssessmentErrors.ComponentTypeExists);
            ResultAssert.Failed(a.AddComponent("Final 2", ComponentType.Final, 10, 50, Due, Now), AssessmentErrors.ComponentTypeExists);
            ResultAssert.Succeeded(a.AddComponent("Quiz 1", ComponentType.Quiz, 10, 20, Due, Now));
            ResultAssert.Succeeded(a.AddComponent("Quiz 2", ComponentType.Quiz, 10, 20, Due, Now));
        }

        [Fact]
        public void Weights_cannot_total_more_than_100()
        {
            var a = New();
            ResultAssert.Succeeded(a.AddComponent("A", ComponentType.Project, 60, 100, Due, Now));
            ResultAssert.Succeeded(a.AddComponent("B", ComponentType.Lab, 40, 100, Due, Now));

            ResultAssert.Failed(a.AddComponent("C", ComponentType.Quiz, 0.01m, 10, Due, Now), AssessmentErrors.WeightExceeded);
            Assert.Equal(100m, a.TotalWeight);
        }

        [Fact]
        public void Updating_a_component_counts_the_other_weights_only()
        {
            var a = New();
            ResultAssert.Succeeded(a.AddComponent("A", ComponentType.Project, 60, 100, Due, Now));
            var b = ResultAssert.Succeeded(a.AddComponent("B", ComponentType.Lab, 40, 100, Due, Now));

            ResultAssert.Failed(a.UpdateComponent(b.Id, "B", ComponentType.Lab, 41, 100, Due, Now), AssessmentErrors.WeightExceeded);
            ResultAssert.Succeeded(a.UpdateComponent(b.Id, "B", ComponentType.Lab, 30, 80, Due.AddDays(1), Now));
            Assert.Equal(90m, a.TotalWeight);
            Assert.Equal(80m, b.MaxScore);

            // Renaming a component to its own name is allowed; to another's is not.
            ResultAssert.Succeeded(a.UpdateComponent(b.Id, "b", ComponentType.Lab, 30, 80, Due, Now));
            ResultAssert.Failed(a.UpdateComponent(b.Id, "A", ComponentType.Lab, 30, 80, Due, Now), AssessmentErrors.ComponentNameExists);
            ResultAssert.Failed(a.UpdateComponent(Guid.NewGuid(), "Z", ComponentType.Lab, 1, 1, Due, Now), AssessmentErrors.ComponentNotFound);
        }

        [Fact]
        public void An_assessment_has_at_most_twenty_components()
        {
            var a = New();
            for (var i = 0; i < Assessment.MaxComponents; i++)
            {
                ResultAssert.Succeeded(a.AddComponent("Q" + i, ComponentType.Quiz, 1, 10, Due, Now));
            }

            ResultAssert.Failed(a.AddComponent("Q-extra", ComponentType.Quiz, 1, 10, Due, Now), AssessmentErrors.TooManyComponents);
        }

        [Fact]
        public void Removing_a_component_frees_its_weight()
        {
            var a = New();
            var first = ResultAssert.Succeeded(a.AddComponent("A", ComponentType.Project, 70, 100, Due, Now));

            ResultAssert.Succeeded(a.RemoveComponent(first.Id, Now));

            Assert.Equal(0m, a.TotalWeight);
            ResultAssert.Failed(a.RemoveComponent(first.Id, Now), AssessmentErrors.ComponentNotFound);
        }

        [Fact]
        public void Publish_needs_components_weights_of_100_and_due_dates()
        {
            var empty = New();
            ResultAssert.Failed(empty.Publish(Now), AssessmentErrors.NoComponents);

            var partial = New();
            ResultAssert.Succeeded(partial.AddComponent("A", ComponentType.Project, 90, 100, Due, Now));
            ResultAssert.Failed(partial.Publish(Now), AssessmentErrors.WeightNotHundred);

            var undated = New();
            ResultAssert.Succeeded(undated.AddComponent("A", ComponentType.Project, 100, 100, null, Now));
            ResultAssert.Failed(undated.Publish(Now), AssessmentErrors.DueDateMissing);

            ResultAssert.Succeeded(Full().Publish(Now));
        }

        [Fact]
        public void Publish_raises_an_event_and_locks_the_scheme()
        {
            var a = Full();
            var someId = a.Components[0].Id;

            ResultAssert.Succeeded(a.Publish(Now));

            Assert.Equal(AssessmentStatus.Published, a.Status);
            Assert.Contains(a.DomainEvents, e => e is AssessmentPublished { ComponentCount: 3 });
            ResultAssert.Failed(a.Publish(Now), AssessmentErrors.InvalidState);
            ResultAssert.Failed(a.AddComponent("New", ComponentType.Quiz, 1, 10, Due, Now), AssessmentErrors.InvalidState);
            ResultAssert.Failed(a.UpdateComponent(someId, "X", ComponentType.Quiz, 1, 10, Due, Now), AssessmentErrors.InvalidState);
            ResultAssert.Failed(a.RemoveComponent(someId, Now), AssessmentErrors.InvalidState);
        }

        [Fact]
        public void Close_needs_a_published_scheme_and_a_closed_one_cannot_be_cancelled()
        {
            var a = Full();
            ResultAssert.Failed(a.Close(Now), AssessmentErrors.InvalidState);

            ResultAssert.Succeeded(a.Publish(Now));
            ResultAssert.Succeeded(a.Close(Now));
            Assert.Contains(a.DomainEvents, e => e is AssessmentClosed);
            ResultAssert.Failed(a.Cancel(Now), AssessmentErrors.InvalidState);
        }

        [Fact]
        public void Cancel_raises_an_event_only_for_a_published_scheme()
        {
            var draft = New();
            ResultAssert.Succeeded(draft.Cancel(Now));
            Assert.DoesNotContain(draft.DomainEvents, e => e is AssessmentCancelled);
            ResultAssert.Failed(draft.Cancel(Now), AssessmentErrors.InvalidState);

            var published = Full();
            ResultAssert.Succeeded(published.Publish(Now));
            ResultAssert.Succeeded(published.Cancel(Now));
            Assert.Contains(published.DomainEvents, e => e is AssessmentCancelled);
        }
    }
}
