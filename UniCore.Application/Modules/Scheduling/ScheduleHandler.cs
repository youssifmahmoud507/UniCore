using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Scheduling.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Application.Modules.Scheduling
{
    // Schedules and their weekly slots. Reading needs Scheduling.Read, every change Scheduling.Manage.
    // Nothing changes in a closed semester (an archive is still allowed there).
    public sealed class ScheduleHandler(
        SchedulingAccess access, ICurrentUser user, IAggregateRepository<Schedule> schedules, IAggregateRepository<CourseSectionSchedule> meetings,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly SchedulingAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Schedule> _schedules = schedules;
        private readonly IAggregateRepository<CourseSectionSchedule> _meetings = meetings;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(Schedule s) => $"Semester={s.AcademicSemesterId};Name={s.Name};Status={s.Status};Slots={s.Slots.Count}";

        private async Task<Result<ScheduleSummary>> CommitAsync(Schedule schedule, string action, string? before, RequestInfo request, CancellationToken cancellationToken, string? extra = null)
        {
            var entry = new AuditEntry(action, _user.UserId, "Schedule", schedule.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(schedule) + extra);
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<ScheduleSummary>.Fail(committed.Errors) : SchedulingMapper.ToSummary(schedule);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<ScheduleSummary>>> ListAsync(PaginationQuery query, string? sort, Guid? semesterId, ScheduleStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ScheduleSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, SchedulesListSpecification.SortFields, "createdAt");
            if (!parsed.TryGetValue(out var option))
            {
                return Result<PagedResult<ScheduleSummary>>.Fail(parsed.Errors);
            }

            var items = await _schedules.ListAsync(new SchedulesListSpecification(query, semesterId, status, option), cancellationToken);
            var total = await _schedules.CountAsync(new SchedulesCountSpecification(semesterId, status), cancellationToken);

            return PagedResult<ScheduleSummary>.Create([.. items.Select(s => SchedulingMapper.ToSummary(s))], query, total);
        }

        public async Task<Result<ScheduleSummary>> GetAsync(Guid scheduleId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(auth.Errors);
            }

            var schedule = await _schedules.FirstOrDefaultAsync(new ScheduleByIdSpecification(scheduleId), cancellationToken);

            return schedule is null ? SchedulingErrors.ScheduleNotFound : SchedulingMapper.ToSummary(schedule);
        }

        // ---------- changes ----------

        public async Task<Result<ScheduleSummary>> CreateAsync(CreateScheduleCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireManageAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(permission.Errors);
            }

            var semester = await _access.OpenSemesterAsync(command.AcademicSemesterId, allowClosed: false, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(semester.Errors);
            }

            var created = Schedule.Create(command.AcademicSemesterId, command.Name, _clock.UtcNow);
            if (!created.TryGetValue(out var schedule))
            {
                return Result<ScheduleSummary>.Fail(created.Errors);
            }

            if (await _schedules.AnyAsync(new ScheduleByNameSpecification(schedule.AcademicSemesterId, schedule.Name), cancellationToken))
            {
                return SchedulingErrors.ScheduleNameExists;
            }

            _schedules.Add(schedule);

            return await CommitAsync(schedule, SchedulingAuditActions.ScheduleCreated, null, request, cancellationToken);
        }

        public async Task<Result<ScheduleSummary>> UpdateAsync(Guid scheduleId, UpdateScheduleCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: false, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result<ScheduleSummary>.Fail(loaded.Errors);
            }

            var before = Describe(schedule);

            // The name is unique inside the semester. Checked before the rename so the loaded schedule is not matched by its own new name.
            var sameName = await _schedules.FirstOrDefaultAsync(new ScheduleByNameSpecification(schedule.AcademicSemesterId, (command.Name ?? string.Empty).Trim()), cancellationToken);
            if (sameName is not null && sameName.Id != schedule.Id)
            {
                return SchedulingErrors.ScheduleNameExists;
            }

            var renamed = schedule.Rename(command.Name, _clock.UtcNow);
            if (renamed.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(renamed.Errors);
            }

            _schedules.Update(schedule);

            return await CommitAsync(schedule, SchedulingAuditActions.ScheduleUpdated, before, request, cancellationToken);
        }

        public async Task<Result<ScheduleSummary>> AddSlotAsync(Guid scheduleId, AddSlotCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: false, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result<ScheduleSummary>.Fail(loaded.Errors);
            }

            var before = Describe(schedule);

            var added = schedule.AddSlot(command.DayOfWeek, command.StartTime, command.EndTime, _clock.UtcNow);
            if (!added.TryGetValue(out var slot))
            {
                return Result<ScheduleSummary>.Fail(added.Errors);
            }

            _schedules.Update(schedule);

            return await CommitAsync(schedule, SchedulingAuditActions.SlotAdded, before, request, cancellationToken, $";Slot={slot.Id} {slot.DayOfWeek} {slot.StartTime}-{slot.EndTime}");
        }

        public async Task<Result<ScheduleSummary>> RemoveSlotAsync(Guid scheduleId, Guid slotId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: false, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result<ScheduleSummary>.Fail(loaded.Errors);
            }

            var before = Describe(schedule);
            var inUse = await _meetings.AnyAsync(new MeetingsInSlotSpecification(slotId), cancellationToken);

            var removed = schedule.RemoveSlot(slotId, inUse, _clock.UtcNow);
            if (removed.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(removed.Errors);
            }

            _schedules.Update(schedule);

            return await CommitAsync(schedule, SchedulingAuditActions.SlotRemoved, before, request, cancellationToken, $";Slot={slotId}");
        }

        public async Task<Result<ScheduleSummary>> PublishAsync(Guid scheduleId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: false, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result<ScheduleSummary>.Fail(loaded.Errors);
            }

            var before = Describe(schedule);

            var published = schedule.Publish(_clock.UtcNow);
            if (published.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(published.Errors);
            }

            _schedules.Update(schedule);

            return await CommitAsync(schedule, SchedulingAuditActions.SchedulePublished, before, request, cancellationToken);
        }

        public async Task<Result<ScheduleSummary>> ArchiveAsync(Guid scheduleId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadForChangeAsync(scheduleId, allowClosedSemester: true, cancellationToken);
            if (!loaded.TryGetValue(out var schedule))
            {
                return Result<ScheduleSummary>.Fail(loaded.Errors);
            }

            var before = Describe(schedule);

            var archived = schedule.Archive(_clock.UtcNow);
            if (archived.IsFailure)
            {
                return Result<ScheduleSummary>.Fail(archived.Errors);
            }

            _schedules.Update(schedule);

            return await CommitAsync(schedule, SchedulingAuditActions.ScheduleArchived, before, request, cancellationToken);
        }
    }
}
