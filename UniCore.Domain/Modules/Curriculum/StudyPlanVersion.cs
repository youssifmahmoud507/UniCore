using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Curriculum
{
    // One version of a study plan: its courses and graduation requirements, with effective dates.
    // Students are linked to a version (StudentAcademicProfile / StudentProgramHistory) and keep it, so an
    // Active or Retired version never changes. To change a plan, create a new draft from it (CreateDraftFrom),
    // edit the draft and activate it. Activating it retires the version it replaces (handler).
    public sealed class StudyPlanVersion : AggregateRoot
    {
        public const int MaxCourses = 200;

        private readonly List<StudyPlanCourse> _courses = [];
        private readonly List<CourseRequirement> _requirements = [];

        private StudyPlanVersion()
        {
        }

        public Guid StudyPlanId { get; private set; }

        // 1, 2, 3 ... inside the plan (database index on StudyPlanId + VersionNumber).
        public int VersionNumber { get; private set; }

        public DateOnly EffectiveFrom { get; private set; }
        public DateOnly? EffectiveTo { get; private set; }
        public StudyPlanVersionStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<StudyPlanCourse> Courses => _courses.AsReadOnly();
        public IReadOnlyList<CourseRequirement> Requirements => _requirements.AsReadOnly();

        // The application layer picks the next version number and checks that the plan is active.
        public static Result<StudyPlanVersion> Create(Guid studyPlanId, int versionNumber, DateOnly effectiveFrom, DateTimeOffset now)
        {
            if (studyPlanId == Guid.Empty)
            {
                return CurriculumErrors.InvalidParent;
            }

            if (versionNumber < 1)
            {
                return CurriculumErrors.VersionNumberInvalid;
            }

            if (effectiveFrom == default)
            {
                return CurriculumErrors.EffectiveDateInvalid;
            }

            return new StudyPlanVersion
            {
                Id = Guid.NewGuid(),
                StudyPlanId = studyPlanId,
                VersionNumber = versionNumber,
                EffectiveFrom = effectiveFrom,
                Status = StudyPlanVersionStatus.Draft,
                CreatedAt = now
            };
        }

        // A new draft that starts as a copy of an existing version (courses and requirements get new ids).
        public static Result<StudyPlanVersion> CreateDraftFrom(StudyPlanVersion source, int versionNumber, DateOnly effectiveFrom, DateTimeOffset now)
        {
            var created = Create(source.StudyPlanId, versionNumber, effectiveFrom, now);
            if (!created.TryGetValue(out var draft))
            {
                return created;
            }

            foreach (var course in source._courses)
            {
                draft._courses.Add(course.CopyTo(draft.Id));
            }

            foreach (var requirement in source._requirements)
            {
                draft._requirements.Add(requirement.CopyTo(draft.Id));
            }

            return draft;
        }

        // ---------- draft editing ----------

        public Result ChangeEffectiveFrom(DateOnly effectiveFrom, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return Result.Fail(CurriculumErrors.VersionNotDraft);
            }

            if (effectiveFrom == default)
            {
                return Result.Fail(CurriculumErrors.EffectiveDateInvalid);
            }

            EffectiveFrom = effectiveFrom;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result<StudyPlanCourse> AddCourse(Guid courseVersionId, Guid? academicLevelId, Guid? recommendedTermId, CourseRequirementType requirementType, int? requiredCredits, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return CurriculumErrors.VersionNotDraft;
            }

            if (_courses.Count >= MaxCourses)
            {
                return CurriculumErrors.TooManyCourses;
            }

            if (_courses.Any(c => c.CourseVersionId == courseVersionId))
            {
                return CurriculumErrors.CourseDuplicate;
            }

            var created = StudyPlanCourse.Create(Id, courseVersionId, academicLevelId, recommendedTermId, requirementType, requiredCredits);
            if (!created.TryGetValue(out var course))
            {
                return created;
            }

            _courses.Add(course);
            UpdatedAt = now;
            return course;
        }

        public Result UpdateCourse(Guid studyPlanCourseId, Guid? academicLevelId, Guid? recommendedTermId, CourseRequirementType requirementType, int? requiredCredits, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return Result.Fail(CurriculumErrors.VersionNotDraft);
            }

            var course = _courses.FirstOrDefault(c => c.Id == studyPlanCourseId);
            if (course is null)
            {
                return Result.Fail(CurriculumErrors.CourseNotFound);
            }

            var applied = course.Apply(academicLevelId, recommendedTermId, requirementType, requiredCredits);
            if (applied.IsFailure)
            {
                return applied;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        // Removing a course also removes its graduation requirement row.
        public Result<StudyPlanCourse> RemoveCourse(Guid studyPlanCourseId, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return CurriculumErrors.VersionNotDraft;
            }

            var course = _courses.FirstOrDefault(c => c.Id == studyPlanCourseId);
            if (course is null)
            {
                return CurriculumErrors.CourseNotFound;
            }

            _courses.Remove(course);
            _requirements.RemoveAll(r => r.CourseVersionId == course.CourseVersionId);
            UpdatedAt = now;
            return course;
        }

        // Adds the graduation rule for a course of this version, or changes it when one exists.
        public Result SetRequirement(Guid courseVersionId, CourseRequirementType requirementType, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return Result.Fail(CurriculumErrors.VersionNotDraft);
            }

            if (!_courses.Any(c => c.CourseVersionId == courseVersionId))
            {
                return Result.Fail(CurriculumErrors.CourseNotFound);
            }

            var existing = _requirements.FirstOrDefault(r => r.CourseVersionId == courseVersionId);
            if (existing is not null)
            {
                var changed = existing.ChangeType(requirementType);
                if (changed.IsFailure)
                {
                    return changed;
                }
            }
            else
            {
                var created = CourseRequirement.Create(Id, courseVersionId, requirementType);
                if (!created.TryGetValue(out var requirement))
                {
                    return Result.Fail(created.Errors);
                }

                _requirements.Add(requirement);
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result<CourseRequirement> RemoveRequirement(Guid courseVersionId, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return CurriculumErrors.VersionNotDraft;
            }

            var requirement = _requirements.FirstOrDefault(r => r.CourseVersionId == courseVersionId);
            if (requirement is null)
            {
                return CurriculumErrors.RequirementNotFound;
            }

            _requirements.Remove(requirement);
            UpdatedAt = now;
            return requirement;
        }

        // ---------- lifecycle ----------

        // Draft -> Active. The caller retires the version this one replaces and checks the dates against it.
        public Result Activate(DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Draft)
            {
                return Result.Fail(CurriculumErrors.VersionInvalidState);
            }

            if (_courses.Count == 0)
            {
                return Result.Fail(CurriculumErrors.VersionEmpty);
            }

            Status = StudyPlanVersionStatus.Active;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Active -> Retired, with the last day it is effective.
        public Result Retire(DateOnly effectiveTo, DateTimeOffset now)
        {
            if (Status != StudyPlanVersionStatus.Active)
            {
                return Result.Fail(CurriculumErrors.VersionInvalidState);
            }

            if (effectiveTo < EffectiveFrom)
            {
                return Result.Fail(CurriculumErrors.EffectiveToInvalid);
            }

            Status = StudyPlanVersionStatus.Retired;
            EffectiveTo = effectiveTo;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- questions ----------

        public bool ContainsCourse(Guid courseVersionId) => _courses.Any(c => c.CourseVersionId == courseVersionId);

        // True for an Active or Retired version whose window covers the date. A Draft is never effective.
        public bool IsEffectiveOn(DateOnly date)
            => Status != StudyPlanVersionStatus.Draft && EffectiveFrom <= date && (EffectiveTo is null || date <= EffectiveTo);

        // The type the graduation check uses for a course: the requirement row when there is one, otherwise the plan course's own type.
        public CourseRequirementType? EffectiveRequirementTypeOf(Guid courseVersionId)
        {
            var rule = _requirements.FirstOrDefault(r => r.CourseVersionId == courseVersionId);
            if (rule is not null)
            {
                return rule.RequirementType;
            }

            return _courses.FirstOrDefault(c => c.CourseVersionId == courseVersionId)?.RequirementType;
        }
    }
}
