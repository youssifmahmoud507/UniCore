using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Scheduling.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Application.Modules.Scheduling
{
    // What other modules (attendance sessions, exams, timetable views) may ask the Scheduling module.
    public interface IScheduleReadService
    {
        // The weekly meetings of a section. Empty when the section is not scheduled yet.
        Task<Result<IReadOnlyList<SectionMeetingInfo>>> GetSectionMeetingsAsync(Guid courseSectionId, CancellationToken cancellationToken = default);
    }

    public sealed record SectionMeetingInfo(Guid MeetingId, Guid CourseSectionId, Guid RoomId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    public sealed class ScheduleReadService(IAggregateRepository<CourseSectionSchedule> meetings) : IScheduleReadService
    {
        private readonly IAggregateRepository<CourseSectionSchedule> _meetings = meetings;

        public async Task<Result<IReadOnlyList<SectionMeetingInfo>>> GetSectionMeetingsAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var list = await _meetings.ListAsync(new MeetingsOfSectionSpecification(courseSectionId), cancellationToken);

            IReadOnlyList<SectionMeetingInfo> info =
                [.. list.OrderBy(m => m.DayOfWeek).ThenBy(m => m.StartTime).Select(m => new SectionMeetingInfo(m.Id, m.CourseSectionId, m.RoomId, m.DayOfWeek, m.StartTime, m.EndTime))];

            return Result<IReadOnlyList<SectionMeetingInfo>>.Ok(info);
        }
    }
}
