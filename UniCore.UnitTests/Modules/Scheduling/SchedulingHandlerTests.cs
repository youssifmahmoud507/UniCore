using UniCore.Application.Modules.Scheduling;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Scheduling;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Scheduling
{
    public class SchedulingHandlerTests
    {
        // ---------- schedules ----------

        [Fact]
        public async Task Create_makes_a_draft_and_audits()
        {
            var env = new SchedulingTestEnv();

            var schedule = await env.NewScheduleAsync("Main");

            Assert.Equal(ScheduleStatus.Draft, schedule.Status);
            Assert.Equal(env.Semester.Id, schedule.AcademicSemesterId);
            Assert.Single(env.Schedules.Items);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == SchedulingAuditActions.ScheduleCreated);
        }

        [Fact]
        public async Task Create_rejects_a_duplicate_name_an_unknown_semester_and_a_closed_semester()
        {
            var env = new SchedulingTestEnv();
            await env.NewScheduleAsync("Main");

            Assert.Equal("SCHEDULE_NAME_EXISTS", (await env.ScheduleHandler.CreateAsync(new CreateScheduleCommand(env.Semester.Id, "Main"), SchedulingTestEnv.Request)).Error?.Code);
            Assert.Equal("SCHEDULE_SEMESTER_NOT_FOUND", (await env.ScheduleHandler.CreateAsync(new CreateScheduleCommand(Guid.NewGuid(), "Other"), SchedulingTestEnv.Request)).Error?.Code);

            var closed = env.SeedSemester(closed: true);
            Assert.Equal("SCHEDULE_SEMESTER_CLOSED", (await env.ScheduleHandler.CreateAsync(new CreateScheduleCommand(closed.Id, "Old"), SchedulingTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task The_same_name_is_fine_in_another_semester()
        {
            var env = new SchedulingTestEnv();
            await env.NewScheduleAsync("Main");

            var other = env.SeedSemester();

            Assert.True((await env.ScheduleHandler.CreateAsync(new CreateScheduleCommand(other.Id, "Main"), SchedulingTestEnv.Request)).IsSuccess);
        }

        [Fact]
        public async Task A_reader_can_read_but_not_change_and_a_stranger_can_do_nothing()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();

            env.AsReader();
            Assert.True((await env.ScheduleHandler.GetAsync(schedule.Id)).IsSuccess);
            Assert.True((await env.ScheduleHandler.ListAsync(new UniCore.Application.Common.Pagination.PaginationQuery(), null, null, null)).IsSuccess);
            Assert.Equal(ErrorType.Forbidden, (await env.ScheduleHandler.CreateAsync(new CreateScheduleCommand(env.Semester.Id, "Other"), SchedulingTestEnv.Request)).Error?.ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await env.ScheduleHandler.PublishAsync(schedule.Id, SchedulingTestEnv.Request)).Error?.ErrorType);

            env.Rbac.SignIn(Guid.NewGuid());
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", (await env.ScheduleHandler.GetAsync(schedule.Id)).Error?.Code);
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", (await env.ScheduleHandler.PublishAsync(Guid.NewGuid(), SchedulingTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task Update_renames_and_keeps_names_unique()
        {
            var env = new SchedulingTestEnv();
            var first = await env.NewScheduleAsync("First");
            await env.NewScheduleAsync("Second");

            var renamed = await env.ScheduleHandler.UpdateAsync(first.Id, new UpdateScheduleCommand("Renamed"), SchedulingTestEnv.Request);
            Assert.Equal("Renamed", renamed.Value!.Name);

            var clash = await env.ScheduleHandler.UpdateAsync(first.Id, new UpdateScheduleCommand("Second"), SchedulingTestEnv.Request);
            Assert.Equal("SCHEDULE_NAME_EXISTS", clash.Error?.Code);

            Assert.Equal("SCHEDULE_NOT_FOUND", (await env.ScheduleHandler.UpdateAsync(Guid.NewGuid(), new UpdateScheduleCommand("x1"), SchedulingTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task Slots_are_added_listed_and_removed_when_unused()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");

            var removed = await env.ScheduleHandler.RemoveSlotAsync(schedule.Id, slot.Id, SchedulingTestEnv.Request);

            Assert.True(removed.IsSuccess, removed.Error?.Code);
            Assert.Empty(removed.Value!.Slots);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == SchedulingAuditActions.SlotRemoved);
        }

        [Fact]
        public async Task A_slot_with_sections_cannot_be_removed()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            await env.AssignOkAsync(schedule, slot, env.SeedSection(), env.SeedRoom());

            env.AsRegistrar();
            var removed = await env.ScheduleHandler.RemoveSlotAsync(schedule.Id, slot.Id, SchedulingTestEnv.Request);

            Assert.Equal("SCHEDULE_SLOT_IN_USE", removed.Error?.Code);
        }

        [Fact]
        public async Task Publish_and_archive_follow_the_lifecycle()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();

            Assert.Equal("SCHEDULE_NEEDS_SLOT", (await env.ScheduleHandler.PublishAsync(schedule.Id, SchedulingTestEnv.Request)).Error?.Code);

            await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var published = await env.ScheduleHandler.PublishAsync(schedule.Id, SchedulingTestEnv.Request);
            Assert.Equal(ScheduleStatus.Published, published.Value!.Status);

            var archived = await env.ScheduleHandler.ArchiveAsync(schedule.Id, SchedulingTestEnv.Request);
            Assert.Equal(ScheduleStatus.Archived, archived.Value!.Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == SchedulingAuditActions.ScheduleArchived);
        }

        [Fact]
        public async Task Nothing_changes_in_a_closed_semester_except_archiving()
        {
            var env = new SchedulingTestEnv();
            var closed = env.SeedSemester(closed: true);
            env.AsRegistrar();
            var schedule = ResultAssert.Succeeded(Schedule.Create(closed.Id, "Old", env.Clock.UtcNow));
            ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, TimeOnly.Parse("09:00"), TimeOnly.Parse("10:30"), env.Clock.UtcNow));
            ResultAssert.Succeeded(schedule.Publish(env.Clock.UtcNow));
            env.Schedules.Items.Add(schedule);

            Assert.Equal("SCHEDULE_SEMESTER_CLOSED", (await env.ScheduleHandler.AddSlotAsync(schedule.Id, new AddSlotCommand(DayOfWeek.Monday, TimeOnly.Parse("09:00"), TimeOnly.Parse("10:00")), SchedulingTestEnv.Request)).Error?.Code);
            Assert.True((await env.ScheduleHandler.ArchiveAsync(schedule.Id, SchedulingTestEnv.Request)).IsSuccess);
        }

        // ---------- placing sections ----------

        [Fact]
        public async Task Assign_places_the_section_touches_the_schedule_and_audits()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var section = env.SeedSection(capacity: 30);
            var room = env.SeedRoom(capacity: 40);

            var meeting = await env.AssignOkAsync(schedule, slot, section, room);

            Assert.Equal(section.SectionId, meeting.CourseSectionId);
            Assert.Equal(room.Id, meeting.RoomId);
            Assert.Equal(DayOfWeek.Sunday, meeting.DayOfWeek);
            Assert.Equal(TimeOnly.Parse("09:00"), meeting.StartTime);
            Assert.Single(env.Meetings.Items);
            Assert.NotNull(env.Schedules.Items[0].UpdatedAt);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == SchedulingAuditActions.SectionScheduled);

            var listed = await env.SectionHandler.ListForScheduleAsync(schedule.Id);
            Assert.Single(listed.Value!);
            Assert.Single((await env.SectionHandler.ListForSectionAsync(section.SectionId)).Value!);
        }

        [Fact]
        public async Task Assign_checks_the_inputs_in_order()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var section = env.SeedSection(capacity: 30);
            var room = env.SeedRoom(capacity: 40);
            env.AsRegistrar();

            async Task<string?> Code(Guid sectionId, Guid slotId, Guid roomId)
                => (await env.SectionHandler.AssignAsync(schedule.Id, new AssignSectionCommand(sectionId, slotId, roomId), SchedulingTestEnv.Request)).Error?.Code;

            Assert.Equal("SECTION_SCHEDULE_INVALID", await Code(Guid.Empty, slot.Id, room.Id));
            Assert.Equal("SCHEDULE_SLOT_NOT_FOUND", await Code(section.SectionId, Guid.NewGuid(), room.Id));
            Assert.Equal("SCHEDULE_SECTION_NOT_FOUND", await Code(Guid.NewGuid(), slot.Id, room.Id));
            Assert.Equal("SCHEDULE_ROOM_NOT_FOUND", await Code(section.SectionId, slot.Id, Guid.NewGuid()));
            Assert.Empty(env.Meetings.Items);
        }

        [Fact]
        public async Task Assign_rejects_a_section_of_another_semester_and_a_cancelled_section()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var room = env.SeedRoom();

            var elsewhere = env.SeedSection(semester: env.SeedSemester());
            Assert.Equal("SCHEDULE_SECTION_SEMESTER_MISMATCH", (await env.AssignAsync(schedule, slot, elsewhere, room)).Error?.Code);

            var cancelled = env.SeedSection(status: CourseSectionStatus.Cancelled);
            Assert.Equal("SCHEDULE_SECTION_NOT_SCHEDULABLE", (await env.AssignAsync(schedule, slot, cancelled, room)).Error?.Code);
        }

        [Fact]
        public async Task A_closed_section_can_still_be_scheduled()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");

            var result = await env.AssignAsync(schedule, slot, env.SeedSection(status: CourseSectionStatus.Closed), env.SeedRoom());

            Assert.True(result.IsSuccess, result.Error?.Code);
        }

        [Fact]
        public async Task Room_must_be_active_and_big_enough()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var section = env.SeedSection(capacity: 50);

            Assert.Equal("ROOM_INACTIVE", (await env.AssignAsync(schedule, slot, section, env.SeedRoom(active: false))).Error?.Code);
            Assert.Equal("ROOM_CAPACITY_EXCEEDED", (await env.AssignAsync(schedule, slot, section, env.SeedRoom(capacity: 49))).Error?.Code);
            Assert.True((await env.AssignAsync(schedule, slot, section, env.SeedRoom(capacity: 50))).IsSuccess);
        }

        [Fact]
        public async Task A_section_cannot_take_the_same_slot_twice_or_overlap_itself()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var overlapping = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:00", "11:30");
            var later = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:30", "12:00");
            var section = env.SeedSection();
            var room = env.SeedRoom();
            await env.AssignOkAsync(schedule, slot, section, room);

            Assert.Equal("SECTION_ALREADY_IN_SLOT", (await env.AssignAsync(schedule, slot, section, room)).Error?.Code);
            Assert.Equal("SECTION_SELF_OVERLAP", (await env.AssignAsync(schedule, overlapping, section, room)).Error?.Code);
            Assert.True((await env.AssignAsync(schedule, later, section, room)).IsSuccess);
        }

        // ---------- room conflict ----------

        [Fact]
        public async Task Two_sections_cannot_share_a_room_at_overlapping_times()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var morning = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var overlap = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:00", "11:30");
            var touching = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:30", "12:00");
            var otherDay = await env.AddSlotAsync(schedule, DayOfWeek.Monday, "09:00", "10:30");
            var room = env.SeedRoom();
            await env.AssignOkAsync(schedule, morning, env.SeedSection(), room);

            Assert.Equal("ROOM_CONFLICT", (await env.AssignAsync(schedule, morning, env.SeedSection(), room)).Error?.Code);
            Assert.Equal("ROOM_CONFLICT", (await env.AssignAsync(schedule, overlap, env.SeedSection(), room)).Error?.Code);
            Assert.True((await env.AssignAsync(schedule, touching, env.SeedSection(), room)).IsSuccess);
            Assert.True((await env.AssignAsync(schedule, otherDay, env.SeedSection(), room)).IsSuccess);
            Assert.True((await env.AssignAsync(schedule, morning, env.SeedSection(), env.SeedRoom())).IsSuccess);
        }

        [Fact]
        public async Task The_same_room_and_time_in_another_semester_is_no_conflict()
        {
            var env = new SchedulingTestEnv();
            var first = await env.NewScheduleAsync("Main");
            var firstSlot = await env.AddSlotAsync(first, DayOfWeek.Sunday, "09:00", "10:30");
            var room = env.SeedRoom();
            await env.AssignOkAsync(first, firstSlot, env.SeedSection(), room);

            var other = env.SeedSemester();
            var second = await env.NewScheduleAsync("Main", other);
            var secondSlot = await env.AddSlotAsync(second, DayOfWeek.Sunday, "09:00", "10:30");

            Assert.True((await env.AssignAsync(second, secondSlot, env.SeedSection(semester: other), room)).IsSuccess);
        }

        [Fact]
        public async Task Unassigning_frees_the_room()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var room = env.SeedRoom();
            var meeting = await env.AssignOkAsync(schedule, slot, env.SeedSection(), room);

            env.AsRegistrar();
            var removed = await env.SectionHandler.UnassignAsync(schedule.Id, meeting.Id, SchedulingTestEnv.Request);

            Assert.True(removed.IsSuccess, removed.Error?.Code);
            Assert.Empty(env.Meetings.Items);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == SchedulingAuditActions.SectionUnscheduled);
            Assert.True((await env.AssignAsync(schedule, slot, env.SeedSection(), room)).IsSuccess);

            env.AsRegistrar();
            Assert.Equal("SECTION_SCHEDULE_NOT_FOUND", (await env.SectionHandler.UnassignAsync(schedule.Id, Guid.NewGuid(), SchedulingTestEnv.Request)).Error?.Code);
        }

        // ---------- instructor rules ----------

        [Fact]
        public async Task An_instructor_cannot_teach_two_sections_at_overlapping_times()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var morning = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var overlap = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:00", "11:30");
            var touching = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:30", "12:00");
            var teacher = env.SeedInstructor();
            await env.AssignOkAsync(schedule, morning, env.SeedSection(teachers: [teacher]), env.SeedRoom());

            var second = env.SeedSection(teachers: [teacher]);

            Assert.Equal("INSTRUCTOR_CONFLICT", (await env.AssignAsync(schedule, morning, second, env.SeedRoom())).Error?.Code);
            Assert.Equal("INSTRUCTOR_CONFLICT", (await env.AssignAsync(schedule, overlap, second, env.SeedRoom())).Error?.Code);
            Assert.True((await env.AssignAsync(schedule, touching, second, env.SeedRoom())).IsSuccess);
        }

        [Fact]
        public async Task A_co_instructor_counts_too()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var main = env.SeedInstructor();
            var assistant = env.SeedInstructor();
            await env.AssignOkAsync(schedule, slot, env.SeedSection(teachers: [env.SeedInstructor(), assistant]), env.SeedRoom());

            var other = env.SeedSection(teachers: [main, assistant]);

            Assert.Equal("INSTRUCTOR_CONFLICT", (await env.AssignAsync(schedule, slot, other, env.SeedRoom())).Error?.Code);
        }

        [Fact]
        public async Task An_instructor_with_windows_can_only_be_placed_inside_them()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var inside = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var outside = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "13:00", "14:30");
            var otherDay = await env.AddSlotAsync(schedule, DayOfWeek.Monday, "09:00", "10:30");
            var teacher = env.SeedInstructor();
            env.AsRegistrar();
            ResultAssert.Succeeded(await env.AvailabilityHandler.AddForInstructorAsync(teacher.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("12:00")), SchedulingTestEnv.Request));

            var section = env.SeedSection(teachers: [teacher]);

            Assert.Equal("INSTRUCTOR_NOT_AVAILABLE", (await env.AssignAsync(schedule, outside, section, env.SeedRoom())).Error?.Code);
            Assert.Equal("INSTRUCTOR_NOT_AVAILABLE", (await env.AssignAsync(schedule, otherDay, section, env.SeedRoom())).Error?.Code);
            Assert.True((await env.AssignAsync(schedule, inside, section, env.SeedRoom())).IsSuccess);
        }

        [Fact]
        public async Task An_instructor_without_windows_is_always_available()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Friday, "20:00", "21:30");

            Assert.True((await env.AssignAsync(schedule, slot, env.SeedSection(teachers: [env.SeedInstructor()]), env.SeedRoom())).IsSuccess);
        }

        // ---------- room availability ----------

        [Fact]
        public async Task A_room_with_windows_can_only_be_used_inside_them()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var inside = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var outside = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "17:00", "18:30");
            var room = env.SeedRoom();
            env.AsRegistrar();
            ResultAssert.Succeeded(await env.AvailabilityHandler.AddForRoomAsync(room.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("16:00")), SchedulingTestEnv.Request));

            Assert.Equal("ROOM_NOT_AVAILABLE", (await env.AssignAsync(schedule, outside, env.SeedSection(), room)).Error?.Code);
            Assert.True((await env.AssignAsync(schedule, inside, env.SeedSection(), room)).IsSuccess);
        }

        // ---------- schedule state ----------

        [Fact]
        public async Task An_archived_schedule_accepts_no_placements()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            env.AsRegistrar();
            await env.ScheduleHandler.PublishAsync(schedule.Id, SchedulingTestEnv.Request);
            await env.ScheduleHandler.ArchiveAsync(schedule.Id, SchedulingTestEnv.Request);

            var result = await env.AssignAsync(schedule, slot, env.SeedSection(), env.SeedRoom());

            Assert.Equal("SCHEDULE_INVALID_STATE", result.Error?.Code);
        }

        [Fact]
        public async Task A_published_schedule_still_accepts_placements()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            env.AsRegistrar();
            await env.ScheduleHandler.PublishAsync(schedule.Id, SchedulingTestEnv.Request);

            Assert.True((await env.AssignAsync(schedule, slot, env.SeedSection(), env.SeedRoom())).IsSuccess);
        }

        [Fact]
        public async Task Only_a_manager_can_place_sections()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var section = env.SeedSection();
            var room = env.SeedRoom();

            env.AsReader();
            var denied = await env.SectionHandler.AssignAsync(schedule.Id, new AssignSectionCommand(section.SectionId, slot.Id, room.Id), SchedulingTestEnv.Request);

            Assert.Equal(ErrorType.Forbidden, denied.Error?.ErrorType);
            Assert.Empty(env.Meetings.Items);
        }

        [Fact]
        public async Task A_concurrency_conflict_on_save_is_reported_and_nothing_is_audited()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var slot = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var auditBefore = env.Rbac.Audit.Entries.Count;
            env.Rbac.Uow.FailWith = CommonErrors.ConcurrencyConflict;

            var result = await env.AssignAsync(schedule, slot, env.SeedSection(), env.SeedRoom());

            Assert.Equal("CONCURRENCY_CONFLICT", result.Error?.Code);
            Assert.Equal(auditBefore, env.Rbac.Audit.Entries.Count);
        }

        // ---------- availability ----------

        [Fact]
        public async Task Availability_windows_are_added_listed_and_removed()
        {
            var env = new SchedulingTestEnv();
            var teacher = env.SeedInstructor();
            env.AsRegistrar();

            var added = await env.AvailabilityHandler.AddForInstructorAsync(teacher.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("12:00")), SchedulingTestEnv.Request);
            Assert.True(added.IsSuccess, added.Error?.Code);
            Assert.Equal(teacher.Id, added.Value!.OwnerId);

            var listed = await env.AvailabilityHandler.ListForInstructorAsync(teacher.Id);
            Assert.Single(listed.Value!);

            env.AsRegistrar();
            var removed = await env.AvailabilityHandler.RemoveForInstructorAsync(teacher.Id, added.Value.Id, SchedulingTestEnv.Request);
            Assert.True(removed.IsSuccess);
            Assert.Empty((await env.AvailabilityHandler.ListForInstructorAsync(teacher.Id)).Value!);
            Assert.Equal("AVAILABILITY_NOT_FOUND", (await env.AvailabilityHandler.RemoveForInstructorAsync(teacher.Id, added.Value.Id, SchedulingTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task Availability_windows_of_one_owner_cannot_overlap_and_the_owner_must_exist()
        {
            var env = new SchedulingTestEnv();
            var room = env.SeedRoom();
            env.AsRegistrar();
            ResultAssert.Succeeded(await env.AvailabilityHandler.AddForRoomAsync(room.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("12:00")), SchedulingTestEnv.Request));

            var overlap = await env.AvailabilityHandler.AddForRoomAsync(room.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("11:00"), TimeOnly.Parse("14:00")), SchedulingTestEnv.Request);
            Assert.Equal("AVAILABILITY_OVERLAP", overlap.Error?.Code);

            var adjacent = await env.AvailabilityHandler.AddForRoomAsync(room.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("12:00"), TimeOnly.Parse("14:00")), SchedulingTestEnv.Request);
            Assert.True(adjacent.IsSuccess, adjacent.Error?.Code);

            Assert.Equal("SCHEDULE_ROOM_NOT_FOUND", (await env.AvailabilityHandler.AddForRoomAsync(Guid.NewGuid(), new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("12:00")), SchedulingTestEnv.Request)).Error?.Code);
            Assert.Equal("AVAILABILITY_INSTRUCTOR_NOT_FOUND", (await env.AvailabilityHandler.AddForInstructorAsync(Guid.NewGuid(), new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("12:00")), SchedulingTestEnv.Request)).Error?.Code);
            Assert.Equal("SCHEDULE_TIME_INVALID", (await env.AvailabilityHandler.AddForRoomAsync(room.Id, new AddAvailabilityCommand(DayOfWeek.Monday, TimeOnly.Parse("12:00"), TimeOnly.Parse("08:00")), SchedulingTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task Availability_needs_manage_to_change_and_read_to_list()
        {
            var env = new SchedulingTestEnv();
            var room = env.SeedRoom();
            env.AsReader();

            Assert.True((await env.AvailabilityHandler.ListForRoomAsync(room.Id)).IsSuccess);
            Assert.Equal(ErrorType.Forbidden, (await env.AvailabilityHandler.AddForRoomAsync(room.Id, new AddAvailabilityCommand(DayOfWeek.Sunday, TimeOnly.Parse("08:00"), TimeOnly.Parse("12:00")), SchedulingTestEnv.Request)).Error?.ErrorType);
        }

        // ---------- student conflict (used by Registration) ----------

        [Fact]
        public async Task Student_conflict_is_true_only_when_a_held_section_overlaps_the_new_one()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var nine = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var ten = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:00", "11:30");
            var eleven = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "10:30", "12:00");
            var held = env.SeedSection();
            var candidate = env.SeedSection();
            var touching = env.SeedSection();
            var unscheduled = env.SeedSection();
            await env.AssignOkAsync(schedule, nine, held, env.SeedRoom());
            await env.AssignOkAsync(schedule, ten, candidate, env.SeedRoom());
            await env.AssignOkAsync(schedule, eleven, touching, env.SeedRoom());

            var student = Guid.NewGuid();
            IReadOnlyCollection<Guid> enrolled = [held.SectionId];

            Assert.True((await env.ConflictService.HasConflictAsync(student, env.Semester.Id, candidate.SectionId, enrolled)).Value);
            Assert.False((await env.ConflictService.HasConflictAsync(student, env.Semester.Id, touching.SectionId, enrolled)).Value);
            Assert.False((await env.ConflictService.HasConflictAsync(student, env.Semester.Id, unscheduled.SectionId, enrolled)).Value);
            Assert.False((await env.ConflictService.HasConflictAsync(student, env.Semester.Id, candidate.SectionId, [])).Value);
            Assert.False((await env.ConflictService.HasConflictAsync(student, env.Semester.Id, candidate.SectionId, [candidate.SectionId])).Value);
        }

        [Fact]
        public async Task A_multi_meeting_section_conflicts_on_any_of_its_meetings()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var sunday = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var tuesday = await env.AddSlotAsync(schedule, DayOfWeek.Tuesday, "09:00", "10:30");
            var held = env.SeedSection();
            var candidate = env.SeedSection();
            await env.AssignOkAsync(schedule, tuesday, held, env.SeedRoom());
            await env.AssignOkAsync(schedule, sunday, candidate, env.SeedRoom());
            await env.AssignOkAsync(schedule, tuesday, candidate, env.SeedRoom());

            Assert.True((await env.ConflictService.HasConflictAsync(Guid.NewGuid(), env.Semester.Id, candidate.SectionId, [held.SectionId])).Value);
        }

        // ---------- the contract other modules use ----------

        [Fact]
        public async Task Read_service_lists_the_meetings_of_a_section_in_week_order()
        {
            var env = new SchedulingTestEnv();
            var schedule = await env.NewScheduleAsync();
            var tuesday = await env.AddSlotAsync(schedule, DayOfWeek.Tuesday, "09:00", "10:30");
            var sunday = await env.AddSlotAsync(schedule, DayOfWeek.Sunday, "09:00", "10:30");
            var section = env.SeedSection();
            await env.AssignOkAsync(schedule, tuesday, section, env.SeedRoom());
            await env.AssignOkAsync(schedule, sunday, section, env.SeedRoom());

            var meetings = (await env.ReadService.GetSectionMeetingsAsync(section.SectionId)).Value!;

            Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Tuesday], meetings.Select(m => m.DayOfWeek));
            Assert.Empty((await env.ReadService.GetSectionMeetingsAsync(Guid.NewGuid())).Value!);
        }
    }
}
