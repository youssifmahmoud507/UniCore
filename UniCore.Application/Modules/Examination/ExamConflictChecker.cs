using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Examination.Specifications;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;

namespace UniCore.Application.Modules.Examination
{
    // Compares one exam with the other exams of the same day: rooms, invigilators, then students.
    // Two exams overlap when they are on the same date and startA < endB && startB < endA (touching times do not).
    // An exam without a date and time cannot conflict.
    public sealed class ExamConflictChecker(IAggregateRepository<Exam> exams, IEnrollmentReadService roster)
    {
        private readonly IAggregateRepository<Exam> _exams = exams;
        private readonly IEnrollmentReadService _roster = roster;

        public async Task<Result> CheckAsync(Exam exam, CancellationToken cancellationToken)
        {
            if (exam.Schedule is not { } schedule)
            {
                return Result.Ok();
            }

            var sameDay = await _exams.ListAsync(new ScheduledExamsOnDateSpecification(schedule.ExamDate, exam.Id), cancellationToken);
            var overlapping = sameDay.Where(o => o.Schedule is not null && schedule.Overlaps(o.Schedule)).ToList();

            if (overlapping.Count == 0)
            {
                return Result.Ok();
            }

            var rooms = exam.Rooms.Select(r => r.RoomId).ToHashSet();
            if (overlapping.Any(o => o.Rooms.Any(r => rooms.Contains(r.RoomId))))
            {
                return Result.Fail(ExaminationErrors.RoomConflict);
            }

            var invigilators = exam.Invigilators.Select(i => i.InstructorId).ToHashSet();
            if (overlapping.Any(o => o.Invigilators.Any(i => invigilators.Contains(i.InstructorId))))
            {
                return Result.Fail(ExaminationErrors.InvigilatorConflict);
            }

            var mine = await _roster.GetRosterAsync(exam.CourseSectionId, cancellationToken);
            if (!mine.TryGetValue(out var myEntries))
            {
                return Result.Fail(mine.Errors);
            }

            var myStudents = myEntries.Select(e => e.StudentId).ToHashSet();
            if (myStudents.Count == 0)
            {
                return Result.Ok();
            }

            foreach (var other in overlapping)
            {
                var theirs = await _roster.GetRosterAsync(other.CourseSectionId, cancellationToken);
                if (!theirs.TryGetValue(out var theirEntries))
                {
                    return Result.Fail(theirs.Errors);
                }

                if (theirEntries.Any(e => myStudents.Contains(e.StudentId)))
                {
                    return Result.Fail(ExaminationErrors.StudentConflict);
                }
            }

            return Result.Ok();
        }
    }
}
