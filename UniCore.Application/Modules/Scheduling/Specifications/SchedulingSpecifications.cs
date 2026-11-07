using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Application.Modules.Scheduling.Specifications
{
    public sealed class ScheduleByIdSpecification : Specification<Schedule>
    {
        public ScheduleByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(s => s.Id == id);
            AddInclude("Slots");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class ScheduleByNameSpecification : Specification<Schedule>
    {
        public ScheduleByNameSpecification(Guid semesterId, string name)
        {
            SetCriteria(s => s.AcademicSemesterId == semesterId && s.Name == name);
        }
    }

    public sealed class SchedulesListSpecification : Specification<Schedule>
    {
        public static readonly string[] SortFields = ["createdAt", "name", "status"];

        public SchedulesListSpecification(PaginationQuery query, Guid? semesterId, ScheduleStatus? status, SortOption sort)
        {
            SetCriteria(s => (semesterId == null || s.AcademicSemesterId == semesterId) && (status == null || s.Status == status));
            AddInclude("Slots");

            switch (sort.Field)
            {
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(s => s.Name); } else { ApplyOrderBy(s => s.Name); }
                    break;
                case "status":
                    if (sort.Descending) { ApplyOrderByDescending(s => s.Status); } else { ApplyOrderBy(s => s.Status); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(s => s.CreatedAt); } else { ApplyOrderBy(s => s.CreatedAt); }
                    break;
            }

            ApplyPaging(query);
        }
    }

    public sealed class SchedulesCountSpecification : Specification<Schedule>
    {
        public SchedulesCountSpecification(Guid? semesterId, ScheduleStatus? status)
        {
            SetCriteria(s => (semesterId == null || s.AcademicSemesterId == semesterId) && (status == null || s.Status == status));
        }
    }

    public sealed class MeetingsOfScheduleSpecification : Specification<CourseSectionSchedule>
    {
        public MeetingsOfScheduleSpecification(Guid scheduleId)
        {
            SetCriteria(m => m.ScheduleId == scheduleId);
        }
    }

    public sealed class MeetingsOfSectionSpecification : Specification<CourseSectionSchedule>
    {
        public MeetingsOfSectionSpecification(Guid sectionId)
        {
            SetCriteria(m => m.CourseSectionId == sectionId);
        }
    }

    public sealed class MeetingsOfSectionsSpecification : Specification<CourseSectionSchedule>
    {
        public MeetingsOfSectionsSpecification(IReadOnlyCollection<Guid> sectionIds)
        {
            var ids = sectionIds.ToList();
            SetCriteria(m => ids.Contains(m.CourseSectionId));
        }
    }

    // Meetings of the given sections inside one semester (the instructor-conflict query).
    public sealed class MeetingsOfSectionsInSemesterSpecification : Specification<CourseSectionSchedule>
    {
        public MeetingsOfSectionsInSemesterSpecification(IReadOnlyCollection<Guid> sectionIds, Guid semesterId)
        {
            var ids = sectionIds.ToList();
            SetCriteria(m => m.AcademicSemesterId == semesterId && ids.Contains(m.CourseSectionId));
        }
    }

    // Everything that happens in one room on one day of a semester (the room-conflict query).
    public sealed class RoomMeetingsSpecification : Specification<CourseSectionSchedule>
    {
        public RoomMeetingsSpecification(Guid semesterId, Guid roomId, DayOfWeek day)
        {
            SetCriteria(m => m.AcademicSemesterId == semesterId && m.RoomId == roomId && m.DayOfWeek == day);
        }
    }

    public sealed class MeetingsInSlotSpecification : Specification<CourseSectionSchedule>
    {
        public MeetingsInSlotSpecification(Guid slotId)
        {
            SetCriteria(m => m.ScheduleSlotId == slotId);
        }
    }

    public sealed class MeetingByIdSpecification : Specification<CourseSectionSchedule>
    {
        public MeetingByIdSpecification(Guid scheduleId, Guid meetingId)
        {
            SetCriteria(m => m.ScheduleId == scheduleId && m.Id == meetingId);
            UseTracking();
        }
    }

    public sealed class InstructorAvailabilitiesSpecification : Specification<InstructorAvailability>
    {
        public InstructorAvailabilitiesSpecification(Guid instructorId)
        {
            SetCriteria(a => a.InstructorId == instructorId);
        }
    }

    public sealed class InstructorAvailabilityByIdSpecification : Specification<InstructorAvailability>
    {
        public InstructorAvailabilityByIdSpecification(Guid instructorId, Guid id)
        {
            SetCriteria(a => a.InstructorId == instructorId && a.Id == id);
            UseTracking();
        }
    }

    public sealed class RoomAvailabilitiesSpecification : Specification<RoomAvailability>
    {
        public RoomAvailabilitiesSpecification(Guid roomId)
        {
            SetCriteria(a => a.RoomId == roomId);
        }
    }

    public sealed class RoomAvailabilityByIdSpecification : Specification<RoomAvailability>
    {
        public RoomAvailabilityByIdSpecification(Guid roomId, Guid id)
        {
            SetCriteria(a => a.RoomId == roomId && a.Id == id);
            UseTracking();
        }
    }
}
