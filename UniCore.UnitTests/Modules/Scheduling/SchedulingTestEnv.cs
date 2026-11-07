using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.People;
using UniCore.Application.Modules.Scheduling;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Scheduling;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Scheduling
{
    // Sections and their instructors answered from dictionaries (the Offerings contract).
    internal sealed class FakeSchedulingOfferings : IOfferingReadService
    {
        public Dictionary<Guid, SectionInfo> Sections { get; } = [];
        public Dictionary<Guid, List<Guid>> Teachers { get; } = [];

        public Task<Result<OfferingInfo>> GetOfferingAsync(Guid offeringId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<OfferingInfo>.Fail(OfferingsErrors.OfferingNotFound));

        public Task<Result<SectionInfo>> GetSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
            => Task.FromResult<Result<SectionInfo>>(Sections.TryGetValue(sectionId, out var info) ? info : OfferingsErrors.SectionNotFound);

        public Task<Result<IReadOnlyList<SectionInstructorInfo>>> GetActiveInstructorsAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SectionInstructorInfo> list = Teachers.TryGetValue(sectionId, out var ids)
                ? [.. ids.Select((id, i) => new SectionInstructorInfo(id, i == 0 ? AssignmentRole.Primary : AssignmentRole.CoInstructor, DateTimeOffset.UtcNow))]
                : [];

            return Task.FromResult(Result<IReadOnlyList<SectionInstructorInfo>>.Ok(list));
        }
    }

    // Which sections each instructor teaches (People's scope-data contract).
    internal sealed class FakeTeaching : ISectionAssignmentReader
    {
        public Dictionary<Guid, List<Guid>> Sections { get; } = [];

        public Task<IReadOnlyList<Guid>> GetAssignedSectionIdsAsync(Guid? instructorId, Guid? teachingAssistantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>(instructorId is { } id && Sections.TryGetValue(id, out var list) ? [.. list] : []);
    }

    // The real scheduling handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    // Fall 2026: 1 Sep 2026 - 31 Jan 2027.
    internal sealed class SchedulingTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<Schedule> Schedules { get; } = new();
        public FakeAggregateRepository<CourseSectionSchedule> Meetings { get; } = new();
        public FakeAggregateRepository<InstructorAvailability> InstructorWindows { get; } = new();
        public FakeAggregateRepository<RoomAvailability> RoomWindows { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeEntityRepository<Room> Rooms { get; } = new();
        public FakeEntityRepository<Instructor> Instructors { get; } = new();
        public FakeSchedulingOfferings Offerings { get; } = new();
        public FakeTeaching Teaching { get; } = new();

        public SchedulingAccess Access { get; }
        public ScheduleHandler ScheduleHandler { get; }
        public SectionScheduleHandler SectionHandler { get; }
        public AvailabilityHandler AvailabilityHandler { get; }
        public ScheduleConflictService ConflictService { get; }
        public ScheduleReadService ReadService { get; }

        public AcademicSemester Semester { get; }

        public SchedulingTestEnv()
        {
            Access = new SchedulingAccess(Rbac.Authorizer, Schedules, Semesters);
            ScheduleHandler = new ScheduleHandler(Access, Rbac.User, Schedules, Meetings, Rbac.Uow, Clock, Rbac.Audit);
            SectionHandler = new SectionScheduleHandler(Access, Rbac.User, Schedules, Meetings, Rooms, Offerings, Teaching, InstructorWindows, RoomWindows, Rbac.Uow, Clock, Rbac.Audit);
            AvailabilityHandler = new AvailabilityHandler(Access, Rbac.User, InstructorWindows, RoomWindows, Instructors, Rooms, Rbac.Uow, Rbac.Audit);
            ConflictService = new ScheduleConflictService(Meetings);
            ReadService = new ScheduleReadService(Meetings);

            Semester = SeedSemester();
        }

        // ---------- who is calling ----------

        public void AsRegistrar()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Registrar, [Permissions.Scheduling.Read, Permissions.Scheduling.Manage], new ScopeGrant(ScopeType.Global, null)));

        public void AsReader()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, [Permissions.Scheduling.Read], new ScopeGrant(ScopeType.Global, null)));

        // ---------- seed data ----------

        public AcademicSemester SeedSemester(bool closed = false)
        {
            var windows = new SemesterWindows(
                RegistrationStart: new(2026, 9, 1), RegistrationEnd: new(2026, 9, 10),
                AddDropStart: new(2026, 9, 5), AddDropEnd: new(2026, 9, 20),
                WithdrawalDeadline: new(2026, 11, 15),
                MidtermStart: new(2026, 11, 1), MidtermEnd: new(2026, 11, 10),
                FinalExamStart: new(2027, 1, 5), FinalExamEnd: new(2027, 1, 20),
                GradeSubmissionDeadline: new(2027, 1, 28), GradePublicationDate: new(2027, 2, 3));

            var semester = ResultAssert.Succeeded(AcademicSemester.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), windows, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31), Clock.UtcNow));

            if (closed)
            {
                foreach (var status in new[] { SemesterStatus.RegistrationOpen, SemesterStatus.InProgress, SemesterStatus.Examinations, SemesterStatus.GradingOpen, SemesterStatus.Closed })
                {
                    ResultAssert.Succeeded(semester.AdvanceTo(status, Clock.UtcNow));
                }
            }

            Semesters.Items.Add(semester);
            return semester;
        }

        public Room SeedRoom(int capacity = 40, bool active = true)
        {
            var room = ResultAssert.Succeeded(Room.Create(Guid.NewGuid(), "R" + Random.Shared.Next(100, 999), null, capacity, RoomType.Classroom));

            if (!active)
            {
                ResultAssert.Succeeded(room.Deactivate());
            }

            Rooms.Items.Add(room);
            return room;
        }

        public Instructor SeedInstructor()
        {
            var instructor = ResultAssert.Succeeded(Instructor.Create(Guid.NewGuid(), AcademicRank.Lecturer, null, Clock.UtcNow));
            Instructors.Items.Add(instructor);
            return instructor;
        }

        // A section known to the Offerings contract. teachers[0] is the primary instructor.
        public SectionInfo SeedSection(int capacity = 30, AcademicSemester? semester = null, CourseSectionStatus status = CourseSectionStatus.Open, params Instructor[] teachers)
        {
            var id = Guid.NewGuid();
            var info = new SectionInfo(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (semester ?? Semester).Id, "A", capacity, status, CourseOfferingStatus.Open, 3, status == CourseSectionStatus.Open);

            Offerings.Sections[id] = info;
            Offerings.Teachers[id] = [.. teachers.Select(t => t.Id)];

            foreach (var teacher in teachers)
            {
                if (!Teaching.Sections.TryGetValue(teacher.Id, out var list))
                {
                    Teaching.Sections[teacher.Id] = list = [];
                }

                list.Add(id);
            }

            return info;
        }

        // ---------- calls ----------

        public async Task<ScheduleSummary> NewScheduleAsync(string name = "Main", AcademicSemester? semester = null)
        {
            AsRegistrar();
            return ResultAssert.Succeeded(await ScheduleHandler.CreateAsync(new CreateScheduleCommand((semester ?? Semester).Id, name), Request));
        }

        public async Task<SlotSummary> AddSlotAsync(ScheduleSummary schedule, DayOfWeek day, string start, string end)
        {
            AsRegistrar();
            var result = await ScheduleHandler.AddSlotAsync(schedule.Id, new AddSlotCommand(day, TimeOnly.Parse(start), TimeOnly.Parse(end)), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!.Slots.First(s => s.DayOfWeek == day && s.StartTime == TimeOnly.Parse(start) && s.EndTime == TimeOnly.Parse(end));
        }

        public Task<Result<MeetingSummary>> AssignAsync(ScheduleSummary schedule, SlotSummary slot, SectionInfo section, Room room)
        {
            AsRegistrar();
            return SectionHandler.AssignAsync(schedule.Id, new AssignSectionCommand(section.SectionId, slot.Id, room.Id), Request);
        }

        public async Task<MeetingSummary> AssignOkAsync(ScheduleSummary schedule, SlotSummary slot, SectionInfo section, Room room)
        {
            var result = await AssignAsync(schedule, slot, section, room);
            Assert.True(result.IsSuccess, result.Error?.Code);
            return result.Value!;
        }
    }
}
