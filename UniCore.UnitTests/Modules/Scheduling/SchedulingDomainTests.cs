using UniCore.Domain.Modules.Scheduling;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Scheduling
{
    public class SchedulingDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
        private static TimeOnly T(string value) => TimeOnly.Parse(value);

        private static Schedule NewSchedule() => ResultAssert.Succeeded(Schedule.Create(Guid.NewGuid(), "Main", Now));

        // ---------- time rules ----------

        [Theory]
        [InlineData("09:00", "10:30", true)]
        [InlineData("09:00", "09:30", true)]
        [InlineData("09:00", "09:29", false)]
        [InlineData("10:00", "09:00", false)]
        [InlineData("09:00", "09:00", false)]
        public void Window_needs_the_end_after_the_start_by_at_least_30_minutes(string start, string end, bool valid)
            => Assert.Equal(valid, TimeRules.IsValidWindow(T(start), T(end)));

        [Fact]
        public void Window_rejects_seconds()
            => Assert.False(TimeRules.IsValidWindow(new TimeOnly(9, 0, 30), new TimeOnly(10, 30)));

        [Theory]
        [InlineData("08:00", "16:00", true)]
        [InlineData("08:00", "16:01", false)]
        public void Slot_is_at_most_8_hours(string start, string end, bool valid)
            => Assert.Equal(valid, TimeRules.IsValidSlot(T(start), T(end)));

        [Theory]
        [InlineData("09:00", "10:30", "10:00", "11:00", true)]
        [InlineData("09:00", "10:30", "10:30", "12:00", false)]
        [InlineData("09:00", "10:30", "07:00", "09:00", false)]
        [InlineData("09:00", "12:00", "10:00", "11:00", true)]
        [InlineData("10:00", "11:00", "09:00", "12:00", true)]
        public void Overlap_is_strict_so_touching_ranges_do_not_overlap(string a1, string a2, string b1, string b2, bool overlaps)
            => Assert.Equal(overlaps, TimeRules.Overlaps(T(a1), T(a2), T(b1), T(b2)));

        [Theory]
        [InlineData("08:00", "12:00", "09:00", "10:00", true)]
        [InlineData("08:00", "12:00", "08:00", "12:00", true)]
        [InlineData("08:00", "12:00", "11:00", "13:00", false)]
        public void Contains_needs_the_inner_range_fully_inside(string o1, string o2, string i1, string i2, bool inside)
            => Assert.Equal(inside, TimeRules.Contains(T(o1), T(o2), T(i1), T(i2)));

        // ---------- schedule ----------

        [Fact]
        public void Create_starts_as_a_draft_and_validates()
        {
            var schedule = ResultAssert.Succeeded(Schedule.Create(Guid.NewGuid(), "  Fall main  ", Now));

            Assert.Equal(ScheduleStatus.Draft, schedule.Status);
            Assert.Equal("Fall main", schedule.Name);
            Assert.Empty(schedule.Slots);

            Assert.Equal("SCHEDULE_INVALID", Schedule.Create(Guid.Empty, "Main", Now).Error?.Code);
            Assert.Equal("SCHEDULE_INVALID", Schedule.Create(Guid.NewGuid(), "x", Now).Error?.Code);
            Assert.Equal("SCHEDULE_INVALID", Schedule.Create(Guid.NewGuid(), null, Now).Error?.Code);
        }

        [Fact]
        public void AddSlot_validates_and_rejects_a_duplicate()
        {
            var schedule = NewSchedule();

            var slot = ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, T("09:00"), T("10:30"), Now));
            Assert.Equal(schedule.Id, slot.ScheduleId);
            Assert.Same(slot, schedule.FindSlot(slot.Id));

            Assert.Equal("SCHEDULE_SLOT_EXISTS", schedule.AddSlot(DayOfWeek.Sunday, T("09:00"), T("10:30"), Now).Error?.Code);
            Assert.Equal("SCHEDULE_TIME_INVALID", schedule.AddSlot(DayOfWeek.Monday, T("10:00"), T("09:00"), Now).Error?.Code);
            Assert.Equal("SCHEDULE_DAY_INVALID", schedule.AddSlot((DayOfWeek)42, T("09:00"), T("10:00"), Now).Error?.Code);
            Assert.Single(schedule.Slots);
        }

        [Fact]
        public void Overlapping_slots_of_one_schedule_are_allowed_because_they_are_only_time_bands()
        {
            var schedule = NewSchedule();

            ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, T("09:00"), T("10:30"), Now));
            ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, T("10:00"), T("11:30"), Now));

            Assert.Equal(2, schedule.Slots.Count);
        }

        [Fact]
        public void RemoveSlot_refuses_unknown_and_used_slots()
        {
            var schedule = NewSchedule();
            var slot = ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, T("09:00"), T("10:30"), Now));

            Assert.Equal("SCHEDULE_SLOT_NOT_FOUND", schedule.RemoveSlot(Guid.NewGuid(), false, Now).Error?.Code);
            Assert.Equal("SCHEDULE_SLOT_IN_USE", schedule.RemoveSlot(slot.Id, true, Now).Error?.Code);
            Assert.Single(schedule.Slots);

            ResultAssert.Succeeded(schedule.RemoveSlot(slot.Id, false, Now));
            Assert.Empty(schedule.Slots);
        }

        [Fact]
        public void Publish_needs_a_slot_then_archive_is_final()
        {
            var schedule = NewSchedule();

            Assert.Equal("SCHEDULE_NEEDS_SLOT", schedule.Publish(Now).Error?.Code);
            Assert.Equal("SCHEDULE_INVALID_STATE", schedule.Archive(Now).Error?.Code);

            ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, T("09:00"), T("10:30"), Now));
            ResultAssert.Succeeded(schedule.Publish(Now));
            Assert.Equal(ScheduleStatus.Published, schedule.Status);
            Assert.Single(schedule.DomainEvents.OfType<SchedulePublished>());
            Assert.Equal("SCHEDULE_INVALID_STATE", schedule.Publish(Now).Error?.Code);

            ResultAssert.Succeeded(schedule.Archive(Now));
            Assert.False(schedule.CanChange);
            Assert.Equal("SCHEDULE_INVALID_STATE", schedule.AddSlot(DayOfWeek.Monday, T("09:00"), T("10:00"), Now).Error?.Code);
            Assert.Equal("SCHEDULE_INVALID_STATE", schedule.Rename("New name", Now).Error?.Code);
            Assert.Equal("SCHEDULE_INVALID_STATE", schedule.Touch(Now).Error?.Code);
        }

        [Fact]
        public void A_published_schedule_still_accepts_changes()
        {
            var schedule = NewSchedule();
            ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Sunday, T("09:00"), T("10:30"), Now));
            ResultAssert.Succeeded(schedule.Publish(Now));

            ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Monday, T("09:00"), T("10:30"), Now));
            ResultAssert.Succeeded(schedule.Rename("Renamed", Now));
            ResultAssert.Succeeded(schedule.Touch(Now.AddMinutes(1)));
            Assert.Equal(Now.AddMinutes(1), schedule.UpdatedAt);
        }

        // ---------- placement ----------

        [Fact]
        public void A_placement_copies_the_semester_day_and_times_of_the_slot_and_raises_an_event()
        {
            var schedule = NewSchedule();
            var slot = ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Tuesday, T("11:00"), T("12:30"), Now));
            var section = Guid.NewGuid();
            var room = Guid.NewGuid();

            var meeting = ResultAssert.Succeeded(CourseSectionSchedule.Create(section, schedule, slot.Id, room, Now));

            Assert.Equal(schedule.AcademicSemesterId, meeting.AcademicSemesterId);
            Assert.Equal(DayOfWeek.Tuesday, meeting.DayOfWeek);
            Assert.Equal(T("11:00"), meeting.StartTime);
            Assert.Equal(T("12:30"), meeting.EndTime);
            Assert.True(meeting.Overlaps(DayOfWeek.Tuesday, T("12:00"), T("13:00")));
            Assert.False(meeting.Overlaps(DayOfWeek.Tuesday, T("12:30"), T("13:30")));
            Assert.False(meeting.Overlaps(DayOfWeek.Wednesday, T("11:00"), T("12:30")));
            Assert.Equal(section, Assert.Single(meeting.DomainEvents.OfType<SectionScheduled>()).CourseSectionId);
        }

        [Fact]
        public void A_placement_needs_ids_and_a_slot_of_the_schedule()
        {
            var schedule = NewSchedule();
            var slot = ResultAssert.Succeeded(schedule.AddSlot(DayOfWeek.Tuesday, T("11:00"), T("12:30"), Now));

            Assert.Equal("SECTION_SCHEDULE_INVALID", CourseSectionSchedule.Create(Guid.Empty, schedule, slot.Id, Guid.NewGuid(), Now).Error?.Code);
            Assert.Equal("SECTION_SCHEDULE_INVALID", CourseSectionSchedule.Create(Guid.NewGuid(), schedule, slot.Id, Guid.Empty, Now).Error?.Code);
            Assert.Equal("SCHEDULE_SLOT_NOT_FOUND", CourseSectionSchedule.Create(Guid.NewGuid(), schedule, Guid.NewGuid(), Guid.NewGuid(), Now).Error?.Code);
        }

        // ---------- availability ----------

        [Fact]
        public void Availability_validates_and_knows_what_it_covers()
        {
            var window = ResultAssert.Succeeded(InstructorAvailability.Create(Guid.NewGuid(), DayOfWeek.Sunday, T("08:00"), T("12:00")));

            Assert.True(window.Covers(DayOfWeek.Sunday, T("09:00"), T("10:30")));
            Assert.False(window.Covers(DayOfWeek.Sunday, T("11:00"), T("13:00")));
            Assert.False(window.Covers(DayOfWeek.Monday, T("09:00"), T("10:30")));
            Assert.True(window.Overlaps(DayOfWeek.Sunday, T("11:00"), T("13:00")));
            Assert.False(window.Overlaps(DayOfWeek.Sunday, T("12:00"), T("13:00")));

            Assert.Equal("AVAILABILITY_INVALID", InstructorAvailability.Create(Guid.Empty, DayOfWeek.Sunday, T("08:00"), T("12:00")).Error?.Code);
            Assert.Equal("SCHEDULE_TIME_INVALID", InstructorAvailability.Create(Guid.NewGuid(), DayOfWeek.Sunday, T("12:00"), T("08:00")).Error?.Code);
            Assert.Equal("SCHEDULE_DAY_INVALID", RoomAvailability.Create(Guid.NewGuid(), (DayOfWeek)9, T("08:00"), T("12:00")).Error?.Code);
            Assert.Equal("AVAILABILITY_INVALID", RoomAvailability.Create(Guid.Empty, DayOfWeek.Sunday, T("08:00"), T("12:00")).Error?.Code);
        }
    }
}
