using UniCore.Application.Modules.Attendance;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;

namespace UniCore.Application.Modules.Examination
{
    // The students of an exam: the registered students of its section (Registrations), minus the students blocked by the
    // attendance rule of Week 11 (IAttendanceReadService). Blocked students are not on the exam list.
    public sealed class ExamRosterService(IEnrollmentReadService roster, IAttendanceReadService attendance)
    {
        private readonly IEnrollmentReadService _roster = roster;
        private readonly IAttendanceReadService _attendance = attendance;

        public async Task<Result<ExamRosterSummary>> GetAsync(Exam exam, CancellationToken cancellationToken)
        {
            var entries = await _roster.GetRosterAsync(exam.CourseSectionId, cancellationToken);
            if (!entries.TryGetValue(out var list))
            {
                return Result<ExamRosterSummary>.Fail(entries.Errors);
            }

            var students = list.Select(e => e.StudentId).Distinct().ToList();
            var eligible = new List<Guid>();
            var blocked = new List<Guid>();

            foreach (var studentId in students)
            {
                var isBlocked = await _attendance.IsBlockedFromExamAsync(studentId, exam.CourseSectionId, cancellationToken);
                if (!isBlocked.TryGetValue(out var value))
                {
                    return Result<ExamRosterSummary>.Fail(isBlocked.Errors);
                }

                (value ? blocked : eligible).Add(studentId);
            }

            return new ExamRosterSummary(exam.Id, students.Count, eligible, blocked);
        }

        // The seats needed: every registered student (blocked ones included, so a later excuse needs no new room).
        public async Task<Result<int>> CountRegisteredAsync(Guid courseSectionId, CancellationToken cancellationToken)
        {
            var entries = await _roster.GetRosterAsync(courseSectionId, cancellationToken);

            return entries.TryGetValue(out var list) ? Result<int>.Ok(list.Select(e => e.StudentId).Distinct().Count()) : Result<int>.Fail(entries.Errors);
        }
    }
}
