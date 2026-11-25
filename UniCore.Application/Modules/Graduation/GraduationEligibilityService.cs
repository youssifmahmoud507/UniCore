using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Curriculum;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Application.Modules.Transcripts;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Graduation
{
    // Requirements: an active student with a current program; every Required course of the study plan VERSION the student follows
    // (not the latest one) passed; the earned credits of the program; the minimum GPA. Passed courses and the GPA come from the
    // published grades through IGpaCalculationService, so a repeated course counts once and a withdrawn student's grade is not in it.
    public sealed class GraduationEligibilityService(
        IGpaCalculationService gpa, IStudyPlanReadService plans, ICourseCatalogReadService catalog,
        IRepository<AcademicProgram> programs, IAggregateRepository<Student> students) : IGraduationEligibilityService
    {
        private readonly IGpaCalculationService _gpa = gpa;
        private readonly IStudyPlanReadService _plans = plans;
        private readonly ICourseCatalogReadService _catalog = catalog;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IAggregateRepository<Student> _students = students;

        public async Task<Result<GraduationEligibility>> CheckAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);
            if (student is null)
            {
                return GraduationErrors.StudentNotFound;
            }

            var cumulative = await _gpa.GetCumulativeAsync(studentId, cancellationToken);
            if (!cumulative.TryGetValue(out var gpaInfo))
            {
                return Result<GraduationEligibility>.Fail(cumulative.Errors);
            }

            var figures = gpaInfo.Cumulative;
            var unmet = new List<Error>();

            if (student.AcademicStatus != StudentAcademicStatus.Active || student.CurrentProgram is not { } current)
            {
                unmet.Add(GraduationErrors.StudentNotActive);

                return new GraduationEligibility(studentId, false, figures, unmet, []);
            }

            var program = await _programs.GetByIdAsync(current.AcademicProgramId, cancellationToken);
            if (program is null)
            {
                return GraduationErrors.Invalid;
            }

            var required = await RequiredCoursesAsync(current.StudyPlanVersionId, cancellationToken);
            if (!required.TryGetValue(out var requiredCourses))
            {
                return Result<GraduationEligibility>.Fail(required.Errors);
            }

            var passedResult = await _gpa.GetPassedCourseIdsAsync(studentId, cancellationToken);
            if (!passedResult.TryGetValue(out var passed))
            {
                return Result<GraduationEligibility>.Fail(passedResult.Errors);
            }

            var passedSet = passed.ToHashSet();
            var missing = requiredCourses.Where(c => !passedSet.Contains(c.CourseId)).Select(c => c.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();

            if (missing.Count > 0)
            {
                unmet.Add(Error.BusinessRule(GraduationErrors.RequiredCoursesMissing.Code, $"{GraduationErrors.RequiredCoursesMissing.Description} Missing: {string.Join(", ", missing)}."));
            }

            if (figures.EarnedCredits < program.TotalRequiredCredits)
            {
                unmet.Add(GraduationErrors.CreditsInsufficient);
            }

            if (figures.AttemptedCredits == 0 || figures.Gpa < GraduationPolicy.Default.MinGpa)
            {
                unmet.Add(GraduationErrors.GpaBelowMinimum);
            }

            return new GraduationEligibility(studentId, unmet.Count == 0, figures, unmet, missing);
        }

        private sealed record RequiredCourse(Guid CourseId, string Code);

        private async Task<Result<IReadOnlyList<RequiredCourse>>> RequiredCoursesAsync(Guid studyPlanVersionId, CancellationToken cancellationToken)
        {
            var courses = await _plans.GetCoursesAsync(studyPlanVersionId, cancellationToken);
            if (!courses.TryGetValue(out var planCourses))
            {
                return Result<IReadOnlyList<RequiredCourse>>.Fail(courses.Errors);
            }

            var result = new List<RequiredCourse>();

            foreach (var planCourse in planCourses.Where(c => c.RequirementType == CourseRequirementType.Required))
            {
                var version = await _catalog.GetVersionAsync(planCourse.CourseVersionId, cancellationToken);
                if (!version.TryGetValue(out var info))
                {
                    return Result<IReadOnlyList<RequiredCourse>>.Fail(version.Errors);
                }

                result.Add(new RequiredCourse(info.CourseId, info.CourseCode));
            }

            return Result<IReadOnlyList<RequiredCourse>>.Ok(result);
        }
    }
}
