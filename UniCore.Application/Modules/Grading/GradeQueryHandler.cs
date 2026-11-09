using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Grading
{
    // Reading grades (Grades.Read). Staff read a section through the Section scope (its instructor) or the department of its course
    // (head, dean, registrar). A student reads only their own PUBLISHED or LOCKED grades; anything not yet published is "not found" to them.
    public sealed class GradeQueryHandler(GradingAccess access, GradingEngine engine, ICurrentUser user, IAggregateRepository<Grade> grades)
    {
        private readonly GradingAccess _access = access;
        private readonly GradingEngine _engine = engine;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Grade> _grades = grades;

        public async Task<Result<GradeSummary>> GetAsync(Guid gradeId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Grades.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<GradeSummary>.Fail(permission.Errors);
            }

            var grade = await _grades.FirstOrDefaultAsync(new GradeByIdSpecification(gradeId), cancellationToken);
            if (grade is null)
            {
                return GradingErrors.NotFound;
            }

            var staff = await _access.AuthorizeSectionAsync(Permissions.Grades.Read, grade.CourseSectionId, openSemester: false, cancellationToken);
            if (staff.IsSuccess)
            {
                return GradingMapper.ToSummary(grade);
            }

            var own = await _access.AuthorizeStudentAsync(Permissions.Grades.Read, grade.StudentId, cancellationToken);
            if (own.IsFailure)
            {
                return Result<GradeSummary>.Fail(staff.Errors);
            }

            return grade.IsFinal ? GradingMapper.ToSummary(grade) : GradingErrors.NotFound;
        }

        // The grade sheet of a section.
        public async Task<Result<PagedResult<GradeSummary>>> ListSectionAsync(Guid sectionId, PaginationQuery query, GradeStatus? status, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Grades.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<PagedResult<GradeSummary>>.Fail(permission.Errors);
            }

            var section = await _access.AuthorizeSectionAsync(Permissions.Grades.Read, sectionId, openSemester: false, cancellationToken);
            if (section.IsFailure)
            {
                return Result<PagedResult<GradeSummary>>.Fail(section.Errors);
            }

            var items = await _grades.ListAsync(new GradesListSpecification(query, sectionId, status), cancellationToken);
            var total = await _grades.CountAsync(new GradesCountSpecification(sectionId, status), cancellationToken);

            return PagedResult<GradeSummary>.Create([.. items.Select(GradingMapper.ToSummary)], query, total);
        }

        // The grades of one student. The student sees only Published and Locked grades; the department of their program sees all.
        public async Task<Result<IReadOnlyList<GradeSummary>>> ListStudentAsync(Guid studentId, Guid? semesterId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Grades.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out var found))
            {
                return Result<IReadOnlyList<GradeSummary>>.Fail(student.Errors);
            }

            var isOwner = found.UserId is { } owner && owner == _user.UserId;
            var items = await _grades.ListAsync(new GradesOfStudentSpecification(studentId, semesterId, finalOnly: isOwner), cancellationToken);

            return Result<IReadOnlyList<GradeSummary>>.Ok([.. items.Select(GradingMapper.ToSummary)]);
        }

        public async Task<Result<GradeScaleSummary>> GetDefaultScaleAsync(CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Grades.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<GradeScaleSummary>.Fail(permission.Errors);
            }

            var scale = await _engine.GetDefaultScaleAsync(cancellationToken);

            return scale.TryGetValue(out var found) ? GradingMapper.ToSummary(found) : Result<GradeScaleSummary>.Fail(scale.Errors);
        }
    }
}
