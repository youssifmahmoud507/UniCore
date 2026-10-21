using UniCore.Domain.Modules.Curriculum;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Curriculum
{
    public class StudyPlanTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid ProgramId = Guid.NewGuid();

        private static StudyPlan NewPlan() => ResultAssert.Succeeded(StudyPlan.Create(ProgramId, "cse-2026", "CSE plan 2026", null, Now));

        [Fact]
        public void Create_normalizes_code_and_starts_active()
        {
            var plan = ResultAssert.Succeeded(StudyPlan.Create(ProgramId, " cse-2026 ", " CSE plan 2026 ", " The first plan ", Now));

            Assert.NotEqual(Guid.Empty, plan.Id);
            Assert.Equal(ProgramId, plan.AcademicProgramId);
            Assert.Equal("CSE-2026", plan.Code);
            Assert.Equal("CSE plan 2026", plan.Name);
            Assert.Equal("The first plan", plan.Description);
            Assert.True(plan.IsActive);
            Assert.Equal(Now, plan.CreatedAt);
            Assert.Null(plan.UpdatedAt);
        }

        [Fact]
        public void Create_rejects_bad_input()
        {
            ResultAssert.Failed(StudyPlan.Create(Guid.Empty, "CSE", "Plan", null, Now), CurriculumErrors.InvalidParent);
            ResultAssert.Failed(StudyPlan.Create(ProgramId, null, "Plan", null, Now), CurriculumErrors.InvalidCode);
            ResultAssert.Failed(StudyPlan.Create(ProgramId, "C", "Plan", null, Now), CurriculumErrors.InvalidCode);
            ResultAssert.Failed(StudyPlan.Create(ProgramId, "CSE 1", "Plan", null, Now), CurriculumErrors.InvalidCode);
            ResultAssert.Failed(StudyPlan.Create(ProgramId, "CSE", " ", null, Now), CurriculumErrors.InvalidName);
            ResultAssert.Failed(StudyPlan.Create(ProgramId, "CSE", "Plan", new string('x', 1001), Now), CurriculumErrors.InvalidDescription);
        }

        [Fact]
        public void Update_changes_details_but_not_the_code()
        {
            var plan = NewPlan();

            ResultAssert.Succeeded(plan.UpdateDetails("CSE plan (revised)", "Notes", Now.AddDays(1)));

            Assert.Equal("CSE-2026", plan.Code);
            Assert.Equal("CSE plan (revised)", plan.Name);
            Assert.Equal(Now.AddDays(1), plan.UpdatedAt);
            ResultAssert.Failed(plan.UpdateDetails(" ", null, Now), CurriculumErrors.InvalidName);
        }

        [Fact]
        public void Deactivate_is_refused_while_an_active_version_exists()
        {
            var plan = NewPlan();

            ResultAssert.Failed(plan.Deactivate(hasActiveVersion: true, Now), CurriculumErrors.PlanHasActiveVersion);
            Assert.True(plan.IsActive);

            ResultAssert.Succeeded(plan.Deactivate(hasActiveVersion: false, Now));
            Assert.False(plan.IsActive);
            ResultAssert.Failed(plan.Deactivate(false, Now), CurriculumErrors.PlanAlreadyInactive);

            ResultAssert.Succeeded(plan.Activate(Now));
            ResultAssert.Failed(plan.Activate(Now), CurriculumErrors.PlanAlreadyActive);
        }
    }
}
