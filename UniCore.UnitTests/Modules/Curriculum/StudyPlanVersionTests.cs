using UniCore.Domain.Modules.Curriculum;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Curriculum
{
    public class StudyPlanVersionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly From = new(2026, 9, 1);
        private static readonly Guid PlanId = Guid.NewGuid();

        private static StudyPlanVersion NewDraft(int number = 1) => ResultAssert.Succeeded(StudyPlanVersion.Create(PlanId, number, From, Now));

        private static StudyPlanCourse Add(StudyPlanVersion v, Guid courseVersionId, CourseRequirementType type = CourseRequirementType.Required, int? credits = null)
            => ResultAssert.Succeeded(v.AddCourse(courseVersionId, null, null, type, credits, Now));

        private static StudyPlanVersion ActiveVersion()
        {
            var v = NewDraft();
            Add(v, Guid.NewGuid());
            ResultAssert.Succeeded(v.Activate(Now));
            return v;
        }

        // ---------- create ----------

        [Fact]
        public void Create_makes_an_empty_draft()
        {
            var v = NewDraft(3);

            Assert.Equal(PlanId, v.StudyPlanId);
            Assert.Equal(3, v.VersionNumber);
            Assert.Equal(From, v.EffectiveFrom);
            Assert.Null(v.EffectiveTo);
            Assert.Equal(StudyPlanVersionStatus.Draft, v.Status);
            Assert.Empty(v.Courses);
            Assert.Empty(v.Requirements);
        }

        [Fact]
        public void Create_rejects_bad_input()
        {
            ResultAssert.Failed(StudyPlanVersion.Create(Guid.Empty, 1, From, Now), CurriculumErrors.InvalidParent);
            ResultAssert.Failed(StudyPlanVersion.Create(PlanId, 0, From, Now), CurriculumErrors.VersionNumberInvalid);
            ResultAssert.Failed(StudyPlanVersion.Create(PlanId, 1, default, Now), CurriculumErrors.EffectiveDateInvalid);
        }

        // ---------- courses ----------

        [Fact]
        public void Add_course_stores_the_placement()
        {
            var v = NewDraft();
            var course = Guid.NewGuid();
            var level = Guid.NewGuid();
            var term = Guid.NewGuid();

            var added = ResultAssert.Succeeded(v.AddCourse(course, level, term, CourseRequirementType.Elective, 3, Now));

            Assert.Equal(course, added.CourseVersionId);
            Assert.Equal(level, added.AcademicLevelId);
            Assert.Equal(term, added.RecommendedTermId);
            Assert.Equal(CourseRequirementType.Elective, added.RequirementType);
            Assert.Equal(3, added.RequiredCredits);
            Assert.Equal(v.Id, added.StudyPlanVersionId);
            Assert.True(v.ContainsCourse(course));
        }

        [Fact]
        public void Add_course_rejects_duplicates_bad_ids_bad_types_and_bad_credits()
        {
            var v = NewDraft();
            var course = Guid.NewGuid();
            Add(v, course);

            ResultAssert.Failed(v.AddCourse(course, null, null, CourseRequirementType.Required, null, Now), CurriculumErrors.CourseDuplicate);
            ResultAssert.Failed(v.AddCourse(Guid.Empty, null, null, CourseRequirementType.Required, null, Now), CurriculumErrors.CourseVersionInvalid);
            ResultAssert.Failed(v.AddCourse(Guid.NewGuid(), null, null, (CourseRequirementType)99, null, Now), CurriculumErrors.RequirementTypeInvalid);
            ResultAssert.Failed(v.AddCourse(Guid.NewGuid(), null, null, CourseRequirementType.Required, 0, Now), CurriculumErrors.RequiredCreditsInvalid);
            ResultAssert.Failed(v.AddCourse(Guid.NewGuid(), null, null, CourseRequirementType.Required, 31, Now), CurriculumErrors.RequiredCreditsInvalid);
            Assert.Single(v.Courses);
        }

        [Fact]
        public void Add_course_stops_at_the_maximum()
        {
            var v = NewDraft();

            for (var i = 0; i < StudyPlanVersion.MaxCourses; i++)
            {
                Add(v, Guid.NewGuid());
            }

            ResultAssert.Failed(v.AddCourse(Guid.NewGuid(), null, null, CourseRequirementType.Required, null, Now), CurriculumErrors.TooManyCourses);
        }

        [Fact]
        public void Empty_level_and_term_ids_become_null()
        {
            var v = NewDraft();

            var added = ResultAssert.Succeeded(v.AddCourse(Guid.NewGuid(), Guid.Empty, Guid.Empty, CourseRequirementType.Required, null, Now));

            Assert.Null(added.AcademicLevelId);
            Assert.Null(added.RecommendedTermId);
        }

        [Fact]
        public void Update_course_changes_the_placement_and_failed_update_changes_nothing()
        {
            var v = NewDraft();
            var course = Add(v, Guid.NewGuid());
            var level = Guid.NewGuid();

            ResultAssert.Succeeded(v.UpdateCourse(course.Id, level, null, CourseRequirementType.FreeElective, 2, Now));
            Assert.Equal(level, course.AcademicLevelId);
            Assert.Equal(CourseRequirementType.FreeElective, course.RequirementType);

            ResultAssert.Failed(v.UpdateCourse(course.Id, null, null, (CourseRequirementType)99, 2, Now), CurriculumErrors.RequirementTypeInvalid);
            Assert.Equal(CourseRequirementType.FreeElective, course.RequirementType);
            ResultAssert.Failed(v.UpdateCourse(Guid.NewGuid(), null, null, CourseRequirementType.Required, null, Now), CurriculumErrors.CourseNotFound);
        }

        [Fact]
        public void Remove_course_also_removes_its_requirement_row()
        {
            var v = NewDraft();
            var courseVersion = Guid.NewGuid();
            var course = Add(v, courseVersion, CourseRequirementType.Elective);
            ResultAssert.Succeeded(v.SetRequirement(courseVersion, CourseRequirementType.Required, Now));

            var removed = ResultAssert.Succeeded(v.RemoveCourse(course.Id, Now));

            Assert.Equal(courseVersion, removed.CourseVersionId);
            Assert.Empty(v.Courses);
            Assert.Empty(v.Requirements);
            ResultAssert.Failed(v.RemoveCourse(course.Id, Now), CurriculumErrors.CourseNotFound);
        }

        // ---------- requirements ----------

        [Fact]
        public void Set_requirement_adds_then_changes_the_same_row()
        {
            var v = NewDraft();
            var courseVersion = Guid.NewGuid();
            Add(v, courseVersion, CourseRequirementType.Elective);

            ResultAssert.Succeeded(v.SetRequirement(courseVersion, CourseRequirementType.Required, Now));
            ResultAssert.Succeeded(v.SetRequirement(courseVersion, CourseRequirementType.GeneralEducation, Now));

            var rule = Assert.Single(v.Requirements);
            Assert.Equal(CourseRequirementType.GeneralEducation, rule.RequirementType);
            Assert.Equal(CourseRequirementType.GeneralEducation, v.EffectiveRequirementTypeOf(courseVersion));
        }

        [Fact]
        public void Set_requirement_needs_a_course_of_the_version_and_a_valid_type()
        {
            var v = NewDraft();
            var courseVersion = Guid.NewGuid();
            Add(v, courseVersion);

            ResultAssert.Failed(v.SetRequirement(Guid.NewGuid(), CourseRequirementType.Required, Now), CurriculumErrors.CourseNotFound);
            ResultAssert.Failed(v.SetRequirement(courseVersion, (CourseRequirementType)99, Now), CurriculumErrors.RequirementTypeInvalid);
            Assert.Empty(v.Requirements);
        }

        [Fact]
        public void Effective_requirement_type_falls_back_to_the_plan_courses_own_type()
        {
            var v = NewDraft();
            var courseVersion = Guid.NewGuid();
            Add(v, courseVersion, CourseRequirementType.Elective);

            Assert.Equal(CourseRequirementType.Elective, v.EffectiveRequirementTypeOf(courseVersion));
            Assert.Null(v.EffectiveRequirementTypeOf(Guid.NewGuid()));
        }

        [Fact]
        public void Remove_requirement_removes_the_row_and_reports_a_missing_one()
        {
            var v = NewDraft();
            var courseVersion = Guid.NewGuid();
            Add(v, courseVersion);
            ResultAssert.Succeeded(v.SetRequirement(courseVersion, CourseRequirementType.Required, Now));

            ResultAssert.Succeeded(v.RemoveRequirement(courseVersion, Now));
            ResultAssert.Failed(v.RemoveRequirement(courseVersion, Now), CurriculumErrors.RequirementNotFound);
        }

        // ---------- lifecycle ----------

        [Fact]
        public void Activate_needs_a_course_and_a_draft()
        {
            var empty = NewDraft();
            ResultAssert.Failed(empty.Activate(Now), CurriculumErrors.VersionEmpty);

            var v = NewDraft();
            Add(v, Guid.NewGuid());
            ResultAssert.Succeeded(v.Activate(Now));
            Assert.Equal(StudyPlanVersionStatus.Active, v.Status);
            ResultAssert.Failed(v.Activate(Now), CurriculumErrors.VersionInvalidState);
        }

        [Fact]
        public void An_active_version_cannot_be_edited()
        {
            var v = ActiveVersion();
            var existing = v.Courses.Single();

            ResultAssert.Failed(v.AddCourse(Guid.NewGuid(), null, null, CourseRequirementType.Required, null, Now), CurriculumErrors.VersionNotDraft);
            ResultAssert.Failed(v.UpdateCourse(existing.Id, null, null, CourseRequirementType.Elective, null, Now), CurriculumErrors.VersionNotDraft);
            ResultAssert.Failed(v.RemoveCourse(existing.Id, Now), CurriculumErrors.VersionNotDraft);
            ResultAssert.Failed(v.SetRequirement(existing.CourseVersionId, CourseRequirementType.Required, Now), CurriculumErrors.VersionNotDraft);
            ResultAssert.Failed(v.RemoveRequirement(existing.CourseVersionId, Now), CurriculumErrors.VersionNotDraft);
            ResultAssert.Failed(v.ChangeEffectiveFrom(From.AddDays(1), Now), CurriculumErrors.VersionNotDraft);
            Assert.Single(v.Courses);
        }

        [Fact]
        public void Retire_sets_the_end_date_and_freezes_the_version()
        {
            var v = ActiveVersion();

            ResultAssert.Failed(v.Retire(From.AddDays(-1), Now), CurriculumErrors.EffectiveToInvalid);

            ResultAssert.Succeeded(v.Retire(new DateOnly(2027, 8, 31), Now));
            Assert.Equal(StudyPlanVersionStatus.Retired, v.Status);
            Assert.Equal(new DateOnly(2027, 8, 31), v.EffectiveTo);
            ResultAssert.Failed(v.Retire(new DateOnly(2027, 9, 30), Now), CurriculumErrors.VersionInvalidState);
            ResultAssert.Failed(NewDraft().Retire(From, Now), CurriculumErrors.VersionInvalidState);
        }

        [Fact]
        public void Retiring_on_the_first_effective_day_is_allowed()
        {
            ResultAssert.Succeeded(ActiveVersion().Retire(From, Now));
        }

        [Fact]
        public void Effective_dates_decide_when_a_version_applies()
        {
            var draft = NewDraft();
            Assert.False(draft.IsEffectiveOn(From));

            var v = ActiveVersion();
            Assert.False(v.IsEffectiveOn(From.AddDays(-1)));
            Assert.True(v.IsEffectiveOn(From));
            Assert.True(v.IsEffectiveOn(From.AddYears(5)));

            ResultAssert.Succeeded(v.Retire(new DateOnly(2027, 8, 31), Now));
            Assert.True(v.IsEffectiveOn(new DateOnly(2027, 8, 31)));
            Assert.False(v.IsEffectiveOn(new DateOnly(2027, 9, 1)));
        }

        [Fact]
        public void Change_effective_from_works_on_a_draft_only_and_rejects_an_empty_date()
        {
            var v = NewDraft();

            ResultAssert.Succeeded(v.ChangeEffectiveFrom(new DateOnly(2027, 2, 1), Now.AddDays(1)));
            Assert.Equal(new DateOnly(2027, 2, 1), v.EffectiveFrom);
            Assert.Equal(Now.AddDays(1), v.UpdatedAt);
            ResultAssert.Failed(v.ChangeEffectiveFrom(default, Now), CurriculumErrors.EffectiveDateInvalid);
        }

        // ---------- copy ----------

        [Fact]
        public void Create_draft_from_copies_courses_and_requirements_with_new_ids()
        {
            var source = NewDraft();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var level = Guid.NewGuid();
            ResultAssert.Succeeded(source.AddCourse(a, level, null, CourseRequirementType.Required, null, Now));
            Add(source, b, CourseRequirementType.Elective, 3);
            ResultAssert.Succeeded(source.SetRequirement(b, CourseRequirementType.Required, Now));
            ResultAssert.Succeeded(source.Activate(Now));

            var draft = ResultAssert.Succeeded(StudyPlanVersion.CreateDraftFrom(source, 2, new DateOnly(2027, 9, 1), Now));

            Assert.NotEqual(source.Id, draft.Id);
            Assert.Equal(PlanId, draft.StudyPlanId);
            Assert.Equal(2, draft.VersionNumber);
            Assert.Equal(StudyPlanVersionStatus.Draft, draft.Status);
            Assert.Equal(2, draft.Courses.Count);
            Assert.All(draft.Courses, c => Assert.Equal(draft.Id, c.StudyPlanVersionId));
            Assert.Empty(draft.Courses.Select(c => c.Id).Intersect(source.Courses.Select(c => c.Id)));
            Assert.Equal(level, draft.Courses.Single(c => c.CourseVersionId == a).AcademicLevelId);
            Assert.Equal(3, draft.Courses.Single(c => c.CourseVersionId == b).RequiredCredits);
            var rule = Assert.Single(draft.Requirements);
            Assert.Equal(draft.Id, rule.StudyPlanVersionId);
            Assert.Equal(b, rule.CourseVersionId);

            // The copy is independent: editing it leaves the active version alone.
            ResultAssert.Succeeded(draft.RemoveCourse(draft.Courses.First(c => c.CourseVersionId == a).Id, Now));
            Assert.Equal(2, source.Courses.Count);
        }
    }
}
