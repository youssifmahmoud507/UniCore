using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.People;
using UniCore.Application.Modules.Scheduling.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Scheduling;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Scheduling
{
    // Places course sections into the slots of a schedule, in a room. The rules, in this order:
    //   schedule can change -> slot is in the schedule -> section exists, same semester, not cancelled -> room exists,
    //   active, big enough -> the section does not already meet there or overlap itself -> room open at that time ->
    //   ROOM_CONFLICT -> for each instructor of the section: available, then INSTRUCTOR_CONFLICT.
    // The schedule is touched in the same save (RowVersion), so two concurrent placements cannot both pass the checks.
    public sealed class SectionScheduleHandler(
        SchedulingAccess access, ICurrentUser user, IAggregateRepository<Schedule> schedules, IAggregateRepository<CourseSectionSchedule> meetings,
        IRepository<Room> rooms, IOfferingReadService offerings, ISectionAssignmentReader teaching,
        IAggregateRepository<InstructorAvailability> instructorWindows, IAggregateRepository<RoomAvailability> roomWindows,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly SchedulingAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Schedule> _schedules = schedules;
        private readonly IAggregateRepository<CourseSectionSchedule> _meetings = meetings;
        private readonly IRepository<Room> _rooms = rooms;
        private readonly IOfferingReadService _offerings = offerings;
        private readonly ISectionAssignmentReader _teaching = teaching;
        private readonly IAggregateRepository<InstructorAvailability> _instructorWindows = instructorWindows;
        private readonly IAggregateRepository<RoomAvailability> _roomWindows = roomWindows;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // ---------- queries ----------

        public async Task<Result<IReadOnlyList<MeetingSummary>>> ListForScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<MeetingSummary>>.Fail(auth.Errors);
            }

            if (await _schedules.GetByIdAsync(scheduleId, cancellationToken) is null)
            {
                return SchedulingErrors.ScheduleNotFound;
            }

            var list = await _meetings.ListAsync(new MeetingsOfScheduleSpecification(scheduleId), cancellationToken);

            return Result<IReadOnlyList<MeetingSummary>>.Ok(Ordered(list));
        }

        public async Task<Result<IReadOnlyList<MeetingSummary>>> ListForSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<MeetingSummary>>.Fail(auth.Errors);
            }

            var list = await _meetings.ListAsync(new MeetingsOfSectionSpecification(sectionId), cancellationToken);

            return Result<IReadOnlyList<MeetingSummary>>.Ok(Ordered(list));
        }

        private static IReadOnlyList<MeetingSummary> Ordered(IReadOnlyList<CourseSectionSchedule> list)
            => [.. list.OrderBy(m => m.DayOfWeek).ThenBy(m => m.StartTime).Select(m => SchedulingMapper.ToSummary(m))];

        // ---------- place ----------

        public async Task<Result<MeetingSummary>> AssignAsync(Guid scheduleId, AssignSectionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: false, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result<MeetingSummary>.Fail(loaded.Errors);
            }

            if (!schedule.CanChange)
            {
                return SchedulingErrors.ScheduleInvalidState;
            }

            if (command.CourseSectionId == Guid.Empty || command.ScheduleSlotId == Guid.Empty || command.RoomId == Guid.Empty)
            {
                return SchedulingErrors.AssignmentInvalid;
            }

            var slot = schedule.FindSlot(command.ScheduleSlotId);
            if (slot is null)
            {
                return SchedulingErrors.SlotNotFound;
            }

            // The section.
            var foundSection = await _offerings.GetSectionAsync(command.CourseSectionId, cancellationToken);
            if (!foundSection.TryGetValue(out var section))
            {
                return SchedulingErrors.SectionNotFound;
            }

            if (section.AcademicSemesterId != schedule.AcademicSemesterId)
            {
                return SchedulingErrors.SectionSemesterMismatch;
            }

            if (section.SectionStatus == CourseSectionStatus.Cancelled || section.OfferingStatus == CourseOfferingStatus.Cancelled)
            {
                return SchedulingErrors.SectionNotSchedulable;
            }

            // The room.
            var room = await _rooms.GetByIdAsync(command.RoomId, cancellationToken);
            if (room is null)
            {
                return SchedulingErrors.RoomNotFound;
            }

            if (!room.IsActive)
            {
                return SchedulingErrors.RoomInactive;
            }

            if (room.Capacity < section.Capacity)
            {
                return SchedulingErrors.RoomCapacityExceeded;
            }

            // The section itself.
            var own = await _meetings.ListAsync(new MeetingsOfSectionSpecification(section.SectionId), cancellationToken);
            if (own.Any(m => m.ScheduleSlotId == slot.Id))
            {
                return SchedulingErrors.SectionAlreadyInSlot;
            }

            if (own.Any(m => m.Overlaps(slot.DayOfWeek, slot.StartTime, slot.EndTime)))
            {
                return SchedulingErrors.SectionSelfOverlap;
            }

            // The room is open at that time (no windows = always open).
            var roomWindows = await _roomWindows.ListAsync(new RoomAvailabilitiesSpecification(room.Id), cancellationToken);
            if (roomWindows.Count > 0 && !roomWindows.Any(w => w.Covers(slot.DayOfWeek, slot.StartTime, slot.EndTime)))
            {
                return SchedulingErrors.RoomNotAvailable;
            }

            // Room conflict.
            var inRoom = await _meetings.ListAsync(new RoomMeetingsSpecification(schedule.AcademicSemesterId, room.Id, slot.DayOfWeek), cancellationToken);
            if (inRoom.Any(m => m.CourseSectionId != section.SectionId && m.Overlaps(slot.DayOfWeek, slot.StartTime, slot.EndTime)))
            {
                return SchedulingErrors.RoomConflict;
            }

            // Instructors of the section.
            var instructors = await _offerings.GetActiveInstructorsAsync(section.SectionId, cancellationToken);
            if (!instructors.TryGetValue(out var teachers))
            {
                return Result<MeetingSummary>.Fail(instructors.Errors);
            }

            foreach (var instructorId in teachers.Select(t => t.InstructorId).Distinct())
            {
                var windows = await _instructorWindows.ListAsync(new InstructorAvailabilitiesSpecification(instructorId), cancellationToken);
                if (windows.Count > 0 && !windows.Any(w => w.Covers(slot.DayOfWeek, slot.StartTime, slot.EndTime)))
                {
                    return SchedulingErrors.InstructorNotAvailable;
                }

                var taught = (await _teaching.GetAssignedSectionIdsAsync(instructorId, null, cancellationToken)).Where(id => id != section.SectionId).ToList();
                if (taught.Count > 0)
                {
                    var theirs = await _meetings.ListAsync(new MeetingsOfSectionsInSemesterSpecification(taught, schedule.AcademicSemesterId), cancellationToken);
                    if (theirs.Any(m => m.Overlaps(slot.DayOfWeek, slot.StartTime, slot.EndTime)))
                    {
                        return SchedulingErrors.InstructorConflict;
                    }
                }
            }

            var now = _clock.UtcNow;

            var created = CourseSectionSchedule.Create(section.SectionId, schedule, slot.Id, room.Id, now);
            if (!created.TryGetValue(out var meeting))
            {
                return Result<MeetingSummary>.Fail(created.Errors);
            }

            var touched = schedule.Touch(now);
            if (touched.IsFailure)
            {
                return Result<MeetingSummary>.Fail(touched.Errors);
            }

            _meetings.Add(meeting);
            _schedules.Update(schedule);

            var entry = new AuditEntry(SchedulingAuditActions.SectionScheduled, _user.UserId, "CourseSectionSchedule", meeting.Id.ToString(), request.IpAddress, request.UserAgent, null,
                $"Section={meeting.CourseSectionId};Room={meeting.RoomId};{meeting.DayOfWeek} {meeting.StartTime}-{meeting.EndTime}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<MeetingSummary>.Fail(committed.Errors) : SchedulingMapper.ToSummary(meeting);
        }

        public async Task<Result> UnassignAsync(Guid scheduleId, Guid meetingId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: false, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result.Fail(loaded.Errors);
            }

            var meeting = await _meetings.FirstOrDefaultAsync(new MeetingByIdSpecification(scheduleId, meetingId), cancellationToken);
            if (meeting is null)
            {
                return Result.Fail(SchedulingErrors.AssignmentNotFound);
            }

            var touched = schedule.Touch(_clock.UtcNow);
            if (touched.IsFailure)
            {
                return touched;
            }

            _meetings.Remove(meeting);
            _schedules.Update(schedule);

            var entry = new AuditEntry(SchedulingAuditActions.SectionUnscheduled, _user.UserId, "CourseSectionSchedule", meeting.Id.ToString(), request.IpAddress, request.UserAgent,
                $"Section={meeting.CourseSectionId};Room={meeting.RoomId};{meeting.DayOfWeek} {meeting.StartTime}-{meeting.EndTime}", null);

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }
    }
}
