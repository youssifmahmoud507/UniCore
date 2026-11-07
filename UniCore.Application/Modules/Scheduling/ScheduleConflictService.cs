using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Registrations;
using UniCore.Application.Modules.Scheduling.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Application.Modules.Scheduling
{
    // The student-conflict rule used by the Registration pipeline (step 9). It replaces the "never a conflict" default.
    // A section without meetings (not scheduled yet) cannot conflict. Meetings that only touch (one ends when the other
    // starts) do not conflict.
    public sealed class ScheduleConflictService(IAggregateRepository<CourseSectionSchedule> meetings) : IScheduleConflictService
    {
        private readonly IAggregateRepository<CourseSectionSchedule> _meetings = meetings;

        public async Task<Result<bool>> HasConflictAsync(Guid studentId, Guid semesterId, Guid courseSectionId, IReadOnlyCollection<Guid> enrolledSectionIds, CancellationToken cancellationToken = default)
        {
            var candidate = await _meetings.ListAsync(new MeetingsOfSectionSpecification(courseSectionId), cancellationToken);
            if (candidate.Count == 0)
            {
                return false;
            }

            var held = enrolledSectionIds.Where(id => id != courseSectionId).Distinct().ToList();
            if (held.Count == 0)
            {
                return false;
            }

            var heldMeetings = await _meetings.ListAsync(new MeetingsOfSectionsSpecification(held), cancellationToken);

            return candidate.Any(c => heldMeetings.Any(h => h.Overlaps(c.DayOfWeek, c.StartTime, c.EndTime)));
        }
    }
}
