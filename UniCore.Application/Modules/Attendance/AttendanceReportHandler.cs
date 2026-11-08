using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Attendance.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Attendance
{
    // Percentages and warnings. A student reads their own (Attendance.Read + ownership); an instructor or a head reads
    // the section (Attendance.Read on the section or its department). Developer 2's reports and the warnings job use
    // IAttendanceReadService / IAttendanceWarningService directly.
    public sealed class AttendanceReportHandler(AttendanceAccess access, AttendanceEvaluator evaluator, IAggregateRepository<AttendanceWarning> warnings)
    {
        private readonly AttendanceAccess _access = access;
        private readonly AttendanceEvaluator _evaluator = evaluator;
        private readonly IAggregateRepository<AttendanceWarning> _warnings = warnings;

        public async Task<Result<StudentAttendanceSummary>> GetStudentAsync(Guid studentId, Guid sectionId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Attendance.Read, studentId, cancellationToken);
            if (student.IsFailure)
            {
                return Result<StudentAttendanceSummary>.Fail(student.Errors);
            }

            return await _evaluator.GetStudentAttendanceAsync(studentId, sectionId, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<StudentAttendanceSummary>>> GetSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<IReadOnlyList<StudentAttendanceSummary>>.Fail(permission.Errors);
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.Read, sectionId, openSemester: false, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<IReadOnlyList<StudentAttendanceSummary>>.Fail(scope.Errors);
            }

            return await _evaluator.GetSectionReportAsync(sectionId, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<WarningSummary>>> ListStudentWarningsAsync(Guid studentId, Guid? sectionId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Attendance.Read, studentId, cancellationToken);
            if (student.IsFailure)
            {
                return Result<IReadOnlyList<WarningSummary>>.Fail(student.Errors);
            }

            var list = await _warnings.ListAsync(new WarningsOfStudentSpecification(studentId, sectionId), cancellationToken);

            return Result<IReadOnlyList<WarningSummary>>.Ok([.. list.Select(AttendanceMapper.ToSummary)]);
        }

        public async Task<Result<IReadOnlyList<WarningSummary>>> ListSectionWarningsAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<IReadOnlyList<WarningSummary>>.Fail(permission.Errors);
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.Read, sectionId, openSemester: false, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<IReadOnlyList<WarningSummary>>.Fail(scope.Errors);
            }

            var list = await _warnings.ListAsync(new WarningsOfSectionSpecification(sectionId), cancellationToken);

            return Result<IReadOnlyList<WarningSummary>>.Ok([.. list.Select(AttendanceMapper.ToSummary)]);
        }
    }
}
