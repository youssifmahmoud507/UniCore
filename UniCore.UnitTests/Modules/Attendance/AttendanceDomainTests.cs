using UniCore.Domain.Modules.Attendance;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Attendance
{
    public class AttendanceDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

        private static AttendanceSession NewSession()
            => ResultAssert.Succeeded(AttendanceSession.Open(Guid.NewGuid(), new DateOnly(2026, 10, 4), new TimeOnly(9, 0), Guid.NewGuid(), Now));

        // ---------- calculator ----------

        [Fact]
        public void Percentage_counts_present_excused_and_late_within_the_threshold()
        {
            var records = new (AttendanceStatus, int?)[]
            {
                (AttendanceStatus.Present, null), (AttendanceStatus.Excused, null), (AttendanceStatus.Late, 10), (AttendanceStatus.Late, 30), (AttendanceStatus.Absent, null)
            };

            var rate = AttendanceCalculator.Calculate(records, lateThresholdMinutes: 15);

            Assert.Equal(5, rate.ClosedSessions);
            Assert.Equal(3, rate.Attended);
            Assert.Equal(60m, rate.Percentage);
        }

        [Fact]
        public void No_closed_session_means_full_attendance()
            => Assert.Equal(100m, AttendanceCalculator.Calculate([], 15).Percentage);

        [Fact]
        public void Percentage_is_rounded_to_two_decimals()
        {
            var records = new (AttendanceStatus, int?)[] { (AttendanceStatus.Present, null), (AttendanceStatus.Absent, null), (AttendanceStatus.Absent, null) };

            Assert.Equal(33.33m, AttendanceCalculator.Calculate(records, 0).Percentage);
        }

        [Theory]
        [InlineData(90, null)]
        [InlineData(85, null)]
        [InlineData(84.99, WarningLevel.Warning)]
        [InlineData(75, WarningLevel.Warning)]
        [InlineData(74.99, WarningLevel.Critical)]
        [InlineData(0, WarningLevel.Critical)]
        public void Level_follows_the_warning_and_minimum_percentages(double percentage, WarningLevel? expected)
            => Assert.Equal(expected, AttendanceCalculator.Evaluate((decimal)percentage, 75, 85));

        // ---------- policy ----------

        [Fact]
        public void Policy_belongs_to_exactly_one_program_or_course()
        {
            Assert.True(AttendancePolicy.Create(Guid.NewGuid(), null, 75, 85, 15, true, Now).IsSuccess);
            Assert.True(AttendancePolicy.Create(null, Guid.NewGuid(), 75, 85, 15, false, Now).IsSuccess);

            Assert.Equal("ATTENDANCE_POLICY_INVALID", AttendancePolicy.Create(Guid.NewGuid(), Guid.NewGuid(), 75, 85, 15, true, Now).Error?.Code);
            Assert.Equal("ATTENDANCE_POLICY_INVALID", AttendancePolicy.Create(null, null, 75, 85, 15, true, Now).Error?.Code);
        }

        [Theory]
        [InlineData(-1, 85, 15)]
        [InlineData(90, 85, 15)]
        [InlineData(75, 101, 15)]
        [InlineData(75, 85, -1)]
        [InlineData(75, 85, 181)]
        public void Policy_rejects_impossible_numbers(double minimum, double warning, int late)
            => Assert.Equal("ATTENDANCE_POLICY_INVALID", AttendancePolicy.Create(null, Guid.NewGuid(), (decimal)minimum, (decimal)warning, late, true, Now).Error?.Code);

        // ---------- session ----------

        [Fact]
        public void Marking_again_replaces_the_record_and_keeps_one_per_student()
        {
            var session = NewSession();
            var student = Guid.NewGuid();
            var enrollment = Guid.NewGuid();

            ResultAssert.Succeeded(session.Mark(student, enrollment, AttendanceStatus.Absent, null, null, Now));
            var again = ResultAssert.Succeeded(session.Mark(student, enrollment, AttendanceStatus.Late, 10, null, Now));

            Assert.Single(session.Records);
            Assert.Equal(AttendanceStatus.Late, again.Status);
            Assert.Equal(10, again.MinutesLate);
        }

        [Fact]
        public void Late_needs_minutes_and_other_statuses_do_not()
        {
            var session = NewSession();

            Assert.Equal("ATTENDANCE_RECORD_INVALID", session.Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Late, null, null, Now).Error?.Code);
            Assert.Equal("ATTENDANCE_RECORD_INVALID", session.Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Late, 300, null, Now).Error?.Code);
            Assert.Equal("ATTENDANCE_RECORD_INVALID", session.Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Present, 5, null, Now).Error?.Code);
        }

        [Fact]
        public void Excused_cannot_be_marked_directly()
            => Assert.Equal("ATTENDANCE_RECORD_INVALID", NewSession().Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Excused, null, null, Now).Error?.Code);

        [Fact]
        public void Nothing_is_marked_in_a_closed_session_and_it_closes_once()
        {
            var session = NewSession();
            ResultAssert.Succeeded(session.Close(Now));

            Assert.Equal("ATTENDANCE_SESSION_CLOSED", session.Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Present, null, null, Now).Error?.Code);
            Assert.Equal("ATTENDANCE_SESSION_NOT_OPEN", session.Close(Now).Error?.Code);
        }

        [Fact]
        public void An_absence_can_be_excused_but_a_present_record_cannot_and_an_excused_one_is_final()
        {
            var session = NewSession();
            var absent = ResultAssert.Succeeded(session.Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Absent, null, null, Now));
            var present = ResultAssert.Succeeded(session.Mark(Guid.NewGuid(), Guid.NewGuid(), AttendanceStatus.Present, null, null, Now));

            Assert.Equal("ATTENDANCE_EXCUSE_NOT_ABSENCE", session.ExcuseRecord(present.Id).Error?.Code);
            Assert.True(session.ExcuseRecord(absent.Id).IsSuccess);
            Assert.Equal(AttendanceStatus.Excused, absent.Status);

            // Even in an open session the excused record cannot be changed.
            Assert.Equal("ATTENDANCE_RECORD_EXCUSED", session.Mark(absent.StudentId, absent.EnrollmentId, AttendanceStatus.Present, null, null, Now).Error?.Code);
        }

        // ---------- excuse ----------

        [Fact]
        public void Excuse_needs_a_reason_of_five_characters_or_more()
        {
            Assert.Equal("ATTENDANCE_EXCUSE_INVALID", AttendanceExcuse.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "no", null, Now).Error?.Code);
            Assert.Equal("ATTENDANCE_EXCUSE_INVALID", AttendanceExcuse.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, Now).Error?.Code);
        }

        [Fact]
        public void A_rejection_needs_a_note_and_a_reviewed_excuse_is_final()
        {
            var excuse = ResultAssert.Succeeded(AttendanceExcuse.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hospital visit", null, Now));

            Assert.Equal("ATTENDANCE_EXCUSE_NOTE_REQUIRED", excuse.Reject(Guid.NewGuid(), " ", Now).Error?.Code);
            Assert.True(excuse.Reject(Guid.NewGuid(), "No proof", Now).IsSuccess);
            Assert.Equal(ExcuseStatus.Rejected, excuse.Status);
            Assert.Equal("ATTENDANCE_EXCUSE_NOT_PENDING", excuse.Accept(Guid.NewGuid(), null, Now).Error?.Code);
        }

        [Fact]
        public void An_excuse_is_accepted_without_a_note()
        {
            var excuse = ResultAssert.Succeeded(AttendanceExcuse.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hospital visit", null, Now));

            Assert.True(excuse.Accept(Guid.NewGuid(), null, Now).IsSuccess);
            Assert.Equal(ExcuseStatus.Accepted, excuse.Status);
        }

        // ---------- warning ----------

        [Fact]
        public void A_critical_warning_of_a_blocking_policy_blocks_and_raises_the_event()
        {
            var warning = ResultAssert.Succeeded(AttendanceWarning.Issue(Guid.NewGuid(), Guid.NewGuid(), 60m, WarningLevel.Critical, true, Now));

            Assert.True(warning.IsExamBlocking);
            var raised = Assert.IsType<AttendanceWarningIssued>(Assert.Single(warning.DomainEvents));
            Assert.Equal(WarningLevel.Critical, raised.Level);
            Assert.Equal(60m, raised.Percentage);
            Assert.True(raised.IsExamBlocking);
        }

        [Fact]
        public void A_plain_warning_never_blocks()
        {
            var warning = ResultAssert.Succeeded(AttendanceWarning.Issue(Guid.NewGuid(), Guid.NewGuid(), 80m, WarningLevel.Warning, true, Now));

            Assert.False(warning.IsExamBlocking);
        }
    }
}
