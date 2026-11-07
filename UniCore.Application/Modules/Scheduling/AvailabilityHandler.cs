using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Scheduling.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Scheduling;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Scheduling
{
    // The weekly availability windows of instructors and rooms. No windows = available at any time. Windows of one owner
    // never overlap on the same day. Removing a window does not move sections already placed (a registrar fixes those).
    public sealed class AvailabilityHandler(
        SchedulingAccess access, ICurrentUser user,
        IAggregateRepository<InstructorAvailability> instructorWindows, IAggregateRepository<RoomAvailability> roomWindows,
        IRepository<Instructor> instructors, IRepository<Room> rooms, IUnitOfWork unitOfWork, IAuditWriter audit)
    {
        private readonly SchedulingAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<InstructorAvailability> _instructorWindows = instructorWindows;
        private readonly IAggregateRepository<RoomAvailability> _roomWindows = roomWindows;
        private readonly IRepository<Instructor> _instructors = instructors;
        private readonly IRepository<Room> _rooms = rooms;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(DayOfWeek day, TimeOnly start, TimeOnly end) => $"{day} {start}-{end}";

        // ---------- instructors ----------

        public async Task<Result<IReadOnlyList<AvailabilitySummary>>> ListForInstructorAsync(Guid instructorId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<AvailabilitySummary>>.Fail(auth.Errors);
            }

            if (await _instructors.GetByIdAsync(instructorId, cancellationToken) is null)
            {
                return SchedulingErrors.InstructorNotFound;
            }

            var list = await _instructorWindows.ListAsync(new InstructorAvailabilitiesSpecification(instructorId), cancellationToken);

            return Result<IReadOnlyList<AvailabilitySummary>>.Ok([.. list.OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime).Select(a => SchedulingMapper.ToSummary(a))]);
        }

        public async Task<Result<AvailabilitySummary>> AddForInstructorAsync(Guid instructorId, AddAvailabilityCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireManageAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AvailabilitySummary>.Fail(permission.Errors);
            }

            if (await _instructors.GetByIdAsync(instructorId, cancellationToken) is null)
            {
                return SchedulingErrors.InstructorNotFound;
            }

            var created = InstructorAvailability.Create(instructorId, command.DayOfWeek, command.StartTime, command.EndTime);
            if (!created.TryGetValue(out var window))
            {
                return Result<AvailabilitySummary>.Fail(created.Errors);
            }

            var existing = await _instructorWindows.ListAsync(new InstructorAvailabilitiesSpecification(instructorId), cancellationToken);
            if (existing.Any(a => a.Overlaps(window.DayOfWeek, window.StartTime, window.EndTime)))
            {
                return SchedulingErrors.AvailabilityOverlap;
            }

            _instructorWindows.Add(window);

            var entry = new AuditEntry(SchedulingAuditActions.InstructorAvailabilityAdded, _user.UserId, "InstructorAvailability", window.Id.ToString(), request.IpAddress, request.UserAgent, null,
                $"Instructor={instructorId};{Describe(window.DayOfWeek, window.StartTime, window.EndTime)}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<AvailabilitySummary>.Fail(committed.Errors) : SchedulingMapper.ToSummary(window);
        }

        public async Task<Result> RemoveForInstructorAsync(Guid instructorId, Guid availabilityId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireManageAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return permission;
            }

            var window = await _instructorWindows.FirstOrDefaultAsync(new InstructorAvailabilityByIdSpecification(instructorId, availabilityId), cancellationToken);
            if (window is null)
            {
                return Result.Fail(SchedulingErrors.AvailabilityNotFound);
            }

            _instructorWindows.Remove(window);

            var entry = new AuditEntry(SchedulingAuditActions.InstructorAvailabilityRemoved, _user.UserId, "InstructorAvailability", window.Id.ToString(), request.IpAddress, request.UserAgent,
                $"Instructor={instructorId};{Describe(window.DayOfWeek, window.StartTime, window.EndTime)}", null);

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }

        // ---------- rooms ----------

        public async Task<Result<IReadOnlyList<AvailabilitySummary>>> ListForRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireReadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<AvailabilitySummary>>.Fail(auth.Errors);
            }

            if (await _rooms.GetByIdAsync(roomId, cancellationToken) is null)
            {
                return SchedulingErrors.RoomNotFound;
            }

            var list = await _roomWindows.ListAsync(new RoomAvailabilitiesSpecification(roomId), cancellationToken);

            return Result<IReadOnlyList<AvailabilitySummary>>.Ok([.. list.OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime).Select(a => SchedulingMapper.ToSummary(a))]);
        }

        public async Task<Result<AvailabilitySummary>> AddForRoomAsync(Guid roomId, AddAvailabilityCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireManageAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AvailabilitySummary>.Fail(permission.Errors);
            }

            if (await _rooms.GetByIdAsync(roomId, cancellationToken) is null)
            {
                return SchedulingErrors.RoomNotFound;
            }

            var created = RoomAvailability.Create(roomId, command.DayOfWeek, command.StartTime, command.EndTime);
            if (!created.TryGetValue(out var window))
            {
                return Result<AvailabilitySummary>.Fail(created.Errors);
            }

            var existing = await _roomWindows.ListAsync(new RoomAvailabilitiesSpecification(roomId), cancellationToken);
            if (existing.Any(a => a.Overlaps(window.DayOfWeek, window.StartTime, window.EndTime)))
            {
                return SchedulingErrors.AvailabilityOverlap;
            }

            _roomWindows.Add(window);

            var entry = new AuditEntry(SchedulingAuditActions.RoomAvailabilityAdded, _user.UserId, "RoomAvailability", window.Id.ToString(), request.IpAddress, request.UserAgent, null,
                $"Room={roomId};{Describe(window.DayOfWeek, window.StartTime, window.EndTime)}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<AvailabilitySummary>.Fail(committed.Errors) : SchedulingMapper.ToSummary(window);
        }

        public async Task<Result> RemoveForRoomAsync(Guid roomId, Guid availabilityId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireManageAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return permission;
            }

            var window = await _roomWindows.FirstOrDefaultAsync(new RoomAvailabilityByIdSpecification(roomId, availabilityId), cancellationToken);
            if (window is null)
            {
                return Result.Fail(SchedulingErrors.AvailabilityNotFound);
            }

            _roomWindows.Remove(window);

            var entry = new AuditEntry(SchedulingAuditActions.RoomAvailabilityRemoved, _user.UserId, "RoomAvailability", window.Id.ToString(), request.IpAddress, request.UserAgent,
                $"Room={roomId};{Describe(window.DayOfWeek, window.StartTime, window.EndTime)}", null);

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }
    }
}
