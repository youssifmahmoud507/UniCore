using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Scheduling
{
    // The timetable of one semester: a name, a status and its weekly time slots (database index on
    // AcademicSemesterId + Name). It is the aggregate root of the slots. It has a RowVersion and every placement of a
    // section touches it (Touch), so two people placing sections into the same schedule at once cannot both pass the
    // room/instructor checks: the second save fails with a concurrency conflict.
    public sealed class Schedule : AggregateRoot
    {
        private readonly List<ScheduleSlot> _slots = [];

        private Schedule()
        {
        }

        public Guid AcademicSemesterId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public ScheduleStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<ScheduleSlot> Slots => _slots.AsReadOnly();

        public bool CanChange => Status != ScheduleStatus.Archived;

        public static Result<Schedule> Create(Guid academicSemesterId, string? name, DateTimeOffset now)
        {
            var clean = name?.Trim();

            if (academicSemesterId == Guid.Empty || !IsValidName(clean))
            {
                return SchedulingErrors.ScheduleInvalid;
            }

            return new Schedule { Id = Guid.NewGuid(), AcademicSemesterId = academicSemesterId, Name = clean!, Status = ScheduleStatus.Draft, CreatedAt = now };
        }

        public Result Rename(string? name, DateTimeOffset now)
        {
            if (!CanChange)
            {
                return Result.Fail(SchedulingErrors.ScheduleInvalidState);
            }

            var clean = name?.Trim();
            if (!IsValidName(clean))
            {
                return Result.Fail(SchedulingErrors.ScheduleInvalid);
            }

            Name = clean!;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result<ScheduleSlot> AddSlot(DayOfWeek day, TimeOnly start, TimeOnly end, DateTimeOffset now)
        {
            if (!CanChange)
            {
                return SchedulingErrors.ScheduleInvalidState;
            }

            if (!Enum.IsDefined(day))
            {
                return SchedulingErrors.DayInvalid;
            }

            if (!TimeRules.IsValidSlot(start, end))
            {
                return SchedulingErrors.TimeInvalid;
            }

            if (_slots.Any(s => s.DayOfWeek == day && s.StartTime == start && s.EndTime == end))
            {
                return SchedulingErrors.SlotExists;
            }

            var slot = ScheduleSlot.Create(Id, day, start, end);
            _slots.Add(slot);
            UpdatedAt = now;
            return slot;
        }

        // The caller says whether sections are placed in the slot.
        public Result RemoveSlot(Guid slotId, bool inUse, DateTimeOffset now)
        {
            if (!CanChange)
            {
                return Result.Fail(SchedulingErrors.ScheduleInvalidState);
            }

            var slot = _slots.FirstOrDefault(s => s.Id == slotId);
            if (slot is null)
            {
                return Result.Fail(SchedulingErrors.SlotNotFound);
            }

            if (inUse)
            {
                return Result.Fail(SchedulingErrors.SlotInUse);
            }

            _slots.Remove(slot);
            UpdatedAt = now;
            return Result.Ok();
        }

        public ScheduleSlot? FindSlot(Guid slotId) => _slots.FirstOrDefault(s => s.Id == slotId);

        public Result Publish(DateTimeOffset now)
        {
            if (Status != ScheduleStatus.Draft)
            {
                return Result.Fail(SchedulingErrors.ScheduleInvalidState);
            }

            if (_slots.Count == 0)
            {
                return Result.Fail(SchedulingErrors.ScheduleNeedsSlot);
            }

            Status = ScheduleStatus.Published;
            UpdatedAt = now;
            RaiseDomainEvent(new SchedulePublished(Guid.NewGuid(), now, Id, AcademicSemesterId));
            return Result.Ok();
        }

        public Result Archive(DateTimeOffset now)
        {
            if (Status != ScheduleStatus.Published)
            {
                return Result.Fail(SchedulingErrors.ScheduleInvalidState);
            }

            Status = ScheduleStatus.Archived;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Marks the schedule as changed when a section is placed or removed, so the RowVersion check serialises
        // concurrent placements.
        public Result Touch(DateTimeOffset now)
        {
            if (!CanChange)
            {
                return Result.Fail(SchedulingErrors.ScheduleInvalidState);
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        private static bool IsValidName(string? name)
            => name is not null && name.Length >= FieldRules.NameMinLength && name.Length <= FieldRules.NameMaxLength;
    }
}
