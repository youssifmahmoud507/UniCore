using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Registrations
{
    public class RegistrationsDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

        private static Registration NewRegistration() => ResultAssert.Succeeded(Registration.Create(Guid.NewGuid(), Guid.NewGuid(), Now));

        private static Enrollment Enroll(Registration r, Guid? section = null, int credits = 3, string? key = null)
            => ResultAssert.Succeeded(r.Enroll(section ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), credits, key, Now));

        // ---------- registration ----------

        [Fact]
        public void Create_starts_as_a_draft_and_rejects_empty_ids()
        {
            var registration = NewRegistration();

            Assert.Equal(RegistrationStatus.Draft, registration.Status);
            Assert.Empty(registration.Enrollments);
            Assert.Equal(0, registration.RegisteredCredits);

            Assert.Equal("REGISTRATION_REQUEST_INVALID", Registration.Create(Guid.Empty, Guid.NewGuid(), Now).Error?.Code);
            Assert.Equal("REGISTRATION_REQUEST_INVALID", Registration.Create(Guid.NewGuid(), Guid.Empty, Now).Error?.Code);
        }

        [Fact]
        public void Enroll_adds_an_enrollment_counts_credits_and_raises_an_event()
        {
            var registration = NewRegistration();
            var section = Guid.NewGuid();

            var enrollment = Enroll(registration, section, 4, "key-0000-0001");

            Assert.Equal(EnrollmentStatus.Registered, enrollment.Status);
            Assert.Equal(section, enrollment.CourseSectionId);
            Assert.Equal(4, registration.RegisteredCredits);
            Assert.Same(enrollment, registration.FindByKey("key-0000-0001"));

            var created = Assert.Single(registration.DomainEvents.OfType<EnrollmentCreated>());
            Assert.Equal(enrollment.Id, created.EnrollmentId);
            Assert.Equal(4, created.Credits);
        }

        [Fact]
        public void Enroll_in_the_same_section_twice_is_already_enrolled()
        {
            var registration = NewRegistration();
            var section = Guid.NewGuid();
            Enroll(registration, section);

            var again = registration.Enroll(section, Guid.NewGuid(), Guid.NewGuid(), 3, null, Now);

            Assert.Equal("ALREADY_ENROLLED", again.Error?.Code);
            Assert.Single(registration.Enrollments);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Enroll_rejects_bad_input(int credits)
        {
            var registration = NewRegistration();

            Assert.Equal("REGISTRATION_REQUEST_INVALID", registration.Enroll(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), credits, null, Now).Error?.Code);
            Assert.Equal("REGISTRATION_REQUEST_INVALID", registration.Enroll(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 3, null, Now).Error?.Code);
        }

        [Fact]
        public void Drop_keeps_the_row_and_a_later_enroll_reuses_it()
        {
            var registration = NewRegistration();
            var section = Guid.NewGuid();
            var first = Enroll(registration, section);

            ResultAssert.Succeeded(registration.Drop(first.Id, Now));
            Assert.Equal(EnrollmentStatus.Dropped, first.Status);
            Assert.NotNull(first.DroppedAt);
            Assert.Equal(0, registration.RegisteredCredits);
            Assert.Single(registration.DomainEvents.OfType<EnrollmentDropped>(), e => !e.IsWithdrawal);

            var second = ResultAssert.Succeeded(registration.Enroll(section, Guid.NewGuid(), Guid.NewGuid(), 3, "key-0000-0002", Now));

            Assert.Same(first, second);
            Assert.Single(registration.Enrollments);
            Assert.Equal(EnrollmentStatus.Registered, second.Status);
            Assert.Null(second.DroppedAt);
        }

        [Fact]
        public void Withdraw_is_final_and_raises_a_withdrawal_event()
        {
            var registration = NewRegistration();
            var section = Guid.NewGuid();
            var enrollment = Enroll(registration, section);

            ResultAssert.Succeeded(registration.Withdraw(enrollment.Id, Now));

            Assert.Equal(EnrollmentStatus.Withdrawn, enrollment.Status);
            Assert.Single(registration.DomainEvents.OfType<EnrollmentDropped>(), e => e.IsWithdrawal);
            Assert.Equal("ENROLLMENT_INVALID_STATE", registration.Enroll(section, Guid.NewGuid(), Guid.NewGuid(), 3, null, Now).Error?.Code);
        }

        [Fact]
        public void Drop_of_an_unknown_or_already_dropped_enrollment_fails()
        {
            var registration = NewRegistration();
            var enrollment = Enroll(registration);

            Assert.Equal("ENROLLMENT_NOT_FOUND", registration.Drop(Guid.NewGuid(), Now).Error?.Code);

            ResultAssert.Succeeded(registration.Drop(enrollment.Id, Now));
            Assert.Equal("ENROLLMENT_INVALID_STATE", registration.Drop(enrollment.Id, Now).Error?.Code);
            Assert.Equal("ENROLLMENT_INVALID_STATE", registration.Withdraw(enrollment.Id, Now).Error?.Code);
        }

        [Fact]
        public void Submit_needs_a_registered_course_and_works_once()
        {
            var registration = NewRegistration();

            Assert.Equal("REGISTRATION_EMPTY", registration.Submit(Now).Error?.Code);

            Enroll(registration);
            ResultAssert.Succeeded(registration.Submit(Now));
            Assert.Equal(RegistrationStatus.Submitted, registration.Status);
            Assert.NotNull(registration.SubmittedAt);

            Assert.Equal("REGISTRATION_INVALID_STATE", registration.Submit(Now).Error?.Code);

            // Add/drop stays possible after submitting.
            Enroll(registration);
            Assert.Equal(2, registration.RegisteredEnrollments.Count());
        }

        [Fact]
        public void Cancel_drops_every_registered_enrollment_and_closes_the_registration()
        {
            var registration = NewRegistration();
            var a = Enroll(registration);
            var b = Enroll(registration);
            ResultAssert.Succeeded(registration.Drop(a.Id, Now));

            var dropped = registration.Cancel(Now).Value!;

            Assert.Equal(RegistrationStatus.Cancelled, registration.Status);
            Assert.Single(dropped);
            Assert.Same(b, dropped[0]);
            Assert.All(registration.Enrollments, e => Assert.Equal(EnrollmentStatus.Dropped, e.Status));
            Assert.Equal("REGISTRATION_INVALID_STATE", registration.Enroll(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, null, Now).Error?.Code);
            Assert.Equal("REGISTRATION_INVALID_STATE", registration.Cancel(Now).Error?.Code);
        }

        [Fact]
        public void Complete_marks_registered_enrollments_completed_and_freezes_the_registration()
        {
            var registration = NewRegistration();
            var kept = Enroll(registration);
            var dropped = Enroll(registration);
            ResultAssert.Succeeded(registration.Drop(dropped.Id, Now));

            ResultAssert.Succeeded(registration.Complete(Now));

            Assert.Equal(RegistrationStatus.Completed, registration.Status);
            Assert.Equal(EnrollmentStatus.Completed, kept.Status);
            Assert.Equal(EnrollmentStatus.Dropped, dropped.Status);
            Assert.False(registration.AcceptsChanges);
            Assert.Equal("REGISTRATION_INVALID_STATE", registration.Complete(Now).Error?.Code);
        }

        [Theory]
        [InlineData("key-0000-0001", "key-0000-0001")]
        [InlineData("  key-0000-0001  ", "key-0000-0001")]
        [InlineData("short", null)]
        [InlineData("has space inside", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void NormalizeKey_accepts_only_visible_ascii_of_8_to_100_characters(string? input, string? expected)
            => Assert.Equal(expected, Registration.NormalizeKey(input));

        [Fact]
        public void NormalizeKey_rejects_more_than_100_characters()
            => Assert.Null(Registration.NormalizeKey(new string('a', 101)));

        // ---------- seats on the section ----------

        private static CourseSection NewSection(int capacity) => ResultAssert.Succeeded(CourseSection.Create(Guid.NewGuid(), "A", capacity, Now));

        [Fact]
        public void TakeSeat_counts_up_to_the_capacity_then_says_the_section_is_full()
        {
            var section = NewSection(2);

            ResultAssert.Succeeded(section.TakeSeat(Now));
            ResultAssert.Succeeded(section.TakeSeat(Now));

            Assert.Equal(2, section.EnrolledCount);
            ResultAssert.Failed(section.TakeSeat(Now), OfferingsErrors.CourseFull);
            Assert.Equal(2, section.EnrolledCount);
        }

        [Fact]
        public void TakeSeat_needs_an_open_section()
        {
            var section = NewSection(2);
            ResultAssert.Succeeded(section.Close(Now));

            ResultAssert.Failed(section.TakeSeat(Now), OfferingsErrors.SectionNotOpen);
            Assert.Equal(0, section.EnrolledCount);
        }

        [Fact]
        public void ReleaseSeat_gives_the_seat_back_and_never_goes_below_zero()
        {
            var section = NewSection(1);
            ResultAssert.Succeeded(section.TakeSeat(Now));

            ResultAssert.Succeeded(section.ReleaseSeat(Now));
            Assert.Equal(0, section.EnrolledCount);

            ResultAssert.Failed(section.ReleaseSeat(Now), OfferingsErrors.SectionInvalidState);
            ResultAssert.Succeeded(section.TakeSeat(Now));
        }
    }
}
