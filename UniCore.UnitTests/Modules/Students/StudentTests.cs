using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;

namespace UniCore.UnitTests.Modules.Students
{
    public class StudentTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 11);
        private static readonly DateOnly Admission = new(2026, 9, 1);
        private static readonly Guid Actor = Guid.NewGuid();

        private static Student NewStudent(string number = "2026CS0001")
        {
            var result = Student.Create(Guid.NewGuid(), number, Admission, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

            Assert.True(result.TryGetValue(out var student));
            Assert.NotNull(student);
            return student;
        }

        private static Result ChangeStatus(Student student, StudentAcademicStatus status, DateOnly? date = null, string? reason = "because")
            => student.ChangeStatus(status, date ?? Today, reason, Actor, Today, Now);

        // ---------- creation ----------

        [Fact]
        public void Create_starts_an_active_student_with_a_profile_and_an_open_program()
        {
            var student = NewStudent("2026cs0001");

            Assert.Equal("2026CS0001", student.StudentNumber);
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
            Assert.Equal(Admission, student.AdmissionDate);
            Assert.Null(student.UserId);
            Assert.Empty(student.StatusHistory);

            Assert.NotNull(student.Profile);
            Assert.Equal(AcademicStanding.GoodStanding, student.Profile.AcademicStanding);

            var program = Assert.Single(student.ProgramHistory);
            Assert.True(program.IsOpen);
            Assert.Equal(Admission, program.StartDate);
            Assert.Equal(StudentProgramStatus.Active, program.Status);
            Assert.Same(program, student.CurrentProgram);
            Assert.Equal(program.StudyPlanVersionId, student.Profile.CurrentStudyPlanVersionId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("AB CD")]
        [InlineData("AB-12")]
        [InlineData("1234567890123456789012345678901")]
        public void Create_rejects_a_bad_student_number(string? number)
        {
            var result = Student.Create(
                Guid.NewGuid(), number, Admission, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Now);

            Assert.Equal("STUDENT_NUMBER_INVALID", result.Error?.Code);
        }

        [Fact]
        public void Create_rejects_empty_ids_and_impossible_dates()
        {
            var person = Guid.NewGuid();
            var id = Guid.NewGuid();

            var results = new[]
            {
            Student.Create(Guid.Empty, "A1", Admission, null, id, id, id, null, Now),
            Student.Create(person, "A1", Admission, null, Guid.Empty, id, id, null, Now),
            Student.Create(person, "A1", Admission, null, id, Guid.Empty, id, null, Now),
            Student.Create(person, "A1", Admission, null, id, id, Guid.Empty, null, Now),
            Student.Create(person, "A1", Admission, null, id, id, id, Guid.Empty, Now),
            Student.Create(person, "A1", new DateOnly(1800, 1, 1), null, id, id, id, null, Now),
            Student.Create(person, "A1", default, null, id, id, id, null, Now),
            Student.Create(person, "A1", Admission, Admission.AddDays(-1), id, id, id, null, Now)
        };

            Assert.All(results, r => Assert.Equal("STUDENT_DATA_INVALID", r.Error?.Code));
        }

        // ---------- account ----------

        [Fact]
        public void LinkAccount_is_idempotent_for_the_same_account_and_conflicts_for_another()
        {
            var student = NewStudent();
            var user = Guid.NewGuid();

            Assert.True(student.LinkAccount(user, Now).IsSuccess);
            Assert.True(student.LinkAccount(user, Now).IsSuccess);
            Assert.Equal(user, student.UserId);

            Assert.Equal("STUDENT_ACCOUNT_ALREADY_LINKED", student.LinkAccount(Guid.NewGuid(), Now).Error?.Code);
            Assert.Equal(user, student.UserId);
        }

        [Fact]
        public void LinkAccount_rejects_an_empty_user()
            => Assert.Equal("STUDENT_DATA_INVALID", NewStudent().LinkAccount(Guid.Empty, Now).Error?.Code);

        // ---------- status ----------

        [Fact]
        public void ChangeStatus_records_the_change_with_who_when_and_why()
        {
            var student = NewStudent();

            var result = ChangeStatus(student, StudentAcademicStatus.OnLeave, Today, "  family reasons ");

            Assert.True(result.IsSuccess);
            Assert.Equal(StudentAcademicStatus.OnLeave, student.AcademicStatus);

            var entry = Assert.Single(student.StatusHistory);
            Assert.Equal(StudentAcademicStatus.Active, entry.OldStatus);
            Assert.Equal(StudentAcademicStatus.OnLeave, entry.NewStatus);
            Assert.Equal(Today, entry.EffectiveDate);
            Assert.Equal("family reasons", entry.Reason);
            Assert.Equal(Actor, entry.ChangedByUserId);
            Assert.Equal(Now, entry.CreatedAt);
            Assert.Equal(Now, student.UpdatedAt);
        }

        [Theory]
        [InlineData(StudentAcademicStatus.OnLeave)]
        [InlineData(StudentAcademicStatus.Suspended)]
        [InlineData(StudentAcademicStatus.Withdrawn)]
        [InlineData(StudentAcademicStatus.Dismissed)]
        public void These_statuses_need_a_reason(StudentAcademicStatus status)
        {
            var student = NewStudent();

            Assert.Equal("STUDENT_STATUS_REASON_REQUIRED", ChangeStatus(student, status, Today, null).Error?.Code);
            Assert.Equal("STUDENT_STATUS_REASON_REQUIRED", ChangeStatus(student, status, Today, "   ").Error?.Code);
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
            Assert.Empty(student.StatusHistory);
        }

        [Fact]
        public void Graduating_does_not_need_a_reason_sets_the_date_and_completes_the_program()
        {
            var student = NewStudent();

            var result = ChangeStatus(student, StudentAcademicStatus.Graduated, Today, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(Today, student.GraduationDate);
            var program = Assert.Single(student.ProgramHistory);
            Assert.Equal(StudentProgramStatus.Completed, program.Status);
            Assert.Equal(Today, program.EndDate);
            Assert.Null(student.CurrentProgram);
        }

        [Fact]
        public void Dismissal_closes_the_program_as_discontinued()
        {
            var student = NewStudent();

            Assert.True(ChangeStatus(student, StudentAcademicStatus.Dismissed).IsSuccess);

            var program = Assert.Single(student.ProgramHistory);
            Assert.Equal(StudentProgramStatus.Discontinued, program.Status);
            Assert.Null(student.GraduationDate);
        }

        [Fact]
        public void Withdrawal_keeps_the_program_open_and_the_student_can_come_back()
        {
            var student = NewStudent();

            Assert.True(ChangeStatus(student, StudentAcademicStatus.Withdrawn).IsSuccess);
            Assert.NotNull(student.CurrentProgram);

            Assert.True(ChangeStatus(student, StudentAcademicStatus.Active, Today, null).IsSuccess);
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
            Assert.Equal(2, student.StatusHistory.Count);
        }

        [Fact]
        public void The_same_status_is_rejected()
            => Assert.Equal("STUDENT_STATUS_UNCHANGED", ChangeStatus(NewStudent(), StudentAcademicStatus.Active).Error?.Code);

        [Fact]
        public void Terminal_statuses_cannot_be_left()
        {
            var graduated = NewStudent("A1");
            ChangeStatus(graduated, StudentAcademicStatus.Graduated, Today, null);

            var dismissed = NewStudent("A2");
            ChangeStatus(dismissed, StudentAcademicStatus.Dismissed);

            Assert.Equal("STUDENT_STATUS_TRANSITION_INVALID", ChangeStatus(graduated, StudentAcademicStatus.Active, Today, null).Error?.Code);
            Assert.Equal("STUDENT_STATUS_TRANSITION_INVALID", ChangeStatus(dismissed, StudentAcademicStatus.Active, Today, null).Error?.Code);
            Assert.Equal(StudentAcademicStatus.Graduated, graduated.AcademicStatus);
        }

        [Fact]
        public void A_withdrawn_student_can_only_become_active_again()
        {
            var student = NewStudent();
            ChangeStatus(student, StudentAcademicStatus.Withdrawn);

            Assert.Equal("STUDENT_STATUS_TRANSITION_INVALID", ChangeStatus(student, StudentAcademicStatus.OnLeave).Error?.Code);
            Assert.Equal("STUDENT_STATUS_TRANSITION_INVALID", ChangeStatus(student, StudentAcademicStatus.Graduated, Today, null).Error?.Code);
        }

        [Fact]
        public void An_undefined_status_or_an_empty_actor_is_invalid()
        {
            var student = NewStudent();

            var badStatus = student.ChangeStatus((StudentAcademicStatus)99, Today, "x", Actor, Today, Now);
            var noActor = student.ChangeStatus(StudentAcademicStatus.OnLeave, Today, "x", Guid.Empty, Today, Now);

            Assert.Equal("STUDENT_DATA_INVALID", badStatus.Error?.Code);
            Assert.Equal("STUDENT_DATA_INVALID", noActor.Error?.Code);
        }

        [Fact]
        public void A_too_long_reason_is_rejected()
        {
            var result = ChangeStatus(NewStudent(), StudentAcademicStatus.OnLeave, Today, new string('x', 501));

            Assert.Equal("STUDENT_DATA_INVALID", result.Error?.Code);
        }

        [Fact]
        public void Effective_date_rules()
        {
            var student = NewStudent();

            Assert.Equal("STUDENT_EFFECTIVE_DATE_INVALID", ChangeStatus(student, StudentAcademicStatus.OnLeave, Admission.AddDays(-1)).Error?.Code);
            Assert.Equal("STUDENT_EFFECTIVE_DATE_INVALID", ChangeStatus(student, StudentAcademicStatus.OnLeave, Today.AddDays(2)).Error?.Code);

            // Tomorrow is accepted (time zone tolerance).
            Assert.True(ChangeStatus(student, StudentAcademicStatus.OnLeave, Today.AddDays(1)).IsSuccess);

            // Never earlier than the previous change.
            Assert.Equal("STUDENT_EFFECTIVE_DATE_INVALID", ChangeStatus(student, StudentAcademicStatus.Active, Today, null).Error?.Code);
            Assert.True(ChangeStatus(student, StudentAcademicStatus.Active, Today.AddDays(1), null).IsSuccess);
        }

        [Fact]
        public void History_only_grows()
        {
            var student = NewStudent();

            ChangeStatus(student, StudentAcademicStatus.OnLeave, Today.AddDays(-5));
            ChangeStatus(student, StudentAcademicStatus.Active, Today.AddDays(-3), null);
            ChangeStatus(student, StudentAcademicStatus.Suspended, Today.AddDays(-1));

            Assert.Equal(3, student.StatusHistory.Count);
            Assert.Equal(
                new[] { StudentAcademicStatus.Active, StudentAcademicStatus.OnLeave, StudentAcademicStatus.Active },
                student.StatusHistory.Select(h => h.OldStatus));
        }

        // ---------- program ----------

        [Fact]
        public void ChangeProgram_closes_the_old_record_opens_a_new_one_and_updates_the_profile()
        {
            var student = NewStudent();
            var oldProgram = student.CurrentProgram;
            Assert.NotNull(oldProgram);

            var newProgram = Guid.NewGuid();
            var newPlan = Guid.NewGuid();
            var newLevel = Guid.NewGuid();
            var start = Today.AddDays(-2);

            var result = student.ChangeProgram(newProgram, newPlan, newLevel, start, " transfer ", Today, Now);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, student.ProgramHistory.Count);

            Assert.Equal(StudentProgramStatus.Changed, oldProgram.Status);
            Assert.Equal(start, oldProgram.EndDate);

            var current = student.CurrentProgram;
            Assert.NotNull(current);
            Assert.Equal(newProgram, current.AcademicProgramId);
            Assert.Equal(newPlan, current.StudyPlanVersionId);
            Assert.Equal(newLevel, current.AcademicLevelId);
            Assert.Equal(start, current.StartDate);
            Assert.Equal("transfer", current.Reason);
            Assert.Equal(1, student.ProgramHistory.Count(h => h.IsOpen));

            Assert.NotNull(student.Profile);
            Assert.Equal(newPlan, student.Profile.CurrentStudyPlanVersionId);
            Assert.Equal(newLevel, student.Profile.CurrentAcademicLevelId);
            Assert.Equal(Now, student.UpdatedAt);
        }

        [Fact]
        public void A_new_study_plan_version_of_the_same_program_counts_as_a_change()
        {
            var student = NewStudent();
            var current = student.CurrentProgram;
            Assert.NotNull(current);

            var result = student.ChangeProgram(
                current.AcademicProgramId, Guid.NewGuid(), current.AcademicLevelId, Today, "new plan", Today, Now);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, student.ProgramHistory.Count);
        }

        [Fact]
        public void ChangeProgram_rules()
        {
            var student = NewStudent();
            var current = student.CurrentProgram;
            Assert.NotNull(current);

            var other = Guid.NewGuid();

            Assert.Equal("STUDENT_PROGRAM_CHANGE_REASON_REQUIRED",
                student.ChangeProgram(other, other, null, Today, null, Today, Now).Error?.Code);
            Assert.Equal("STUDENT_PROGRAM_CHANGE_REASON_REQUIRED",
                student.ChangeProgram(other, other, null, Today, "  ", Today, Now).Error?.Code);
            Assert.Equal("STUDENT_PROGRAM_UNCHANGED",
                student.ChangeProgram(current.AcademicProgramId, current.StudyPlanVersionId, current.AcademicLevelId, Today, "x", Today, Now).Error?.Code);
            Assert.Equal("STUDENT_EFFECTIVE_DATE_INVALID",
                student.ChangeProgram(other, other, null, Admission.AddDays(-1), "x", Today, Now).Error?.Code);
            Assert.Equal("STUDENT_EFFECTIVE_DATE_INVALID",
                student.ChangeProgram(other, other, null, Today.AddDays(2), "x", Today, Now).Error?.Code);
            Assert.Equal("STUDENT_DATA_INVALID",
                student.ChangeProgram(Guid.Empty, other, null, Today, "x", Today, Now).Error?.Code);
            Assert.Equal("STUDENT_DATA_INVALID",
                student.ChangeProgram(other, other, Guid.Empty, Today, "x", Today, Now).Error?.Code);
            Assert.Equal("STUDENT_DATA_INVALID",
                student.ChangeProgram(other, other, null, Today, new string('x', 501), Today, Now).Error?.Code);

            Assert.Single(student.ProgramHistory);
            Assert.True(current.IsOpen);
        }

        [Fact]
        public void Only_active_students_can_change_program()
        {
            var student = NewStudent();
            ChangeStatus(student, StudentAcademicStatus.OnLeave);

            var result = student.ChangeProgram(Guid.NewGuid(), Guid.NewGuid(), null, Today, "x", Today, Now);

            Assert.Equal("STUDENT_NOT_ACTIVE", result.Error?.Code);
        }

        // ---------- academic profile ----------

        [Fact]
        public void UpdateAcademicProfile_sets_only_what_is_given()
        {
            var student = NewStudent();
            Assert.NotNull(student.Profile);
            var oldLevel = student.Profile.CurrentAcademicLevelId;
            var newLevel = Guid.NewGuid();

            Assert.True(student.UpdateAcademicProfile(null, AcademicStanding.Probation, Now).IsSuccess);
            Assert.Equal(AcademicStanding.Probation, student.Profile.AcademicStanding);
            Assert.Equal(oldLevel, student.Profile.CurrentAcademicLevelId);

            Assert.True(student.UpdateAcademicProfile(newLevel, null, Now).IsSuccess);
            Assert.Equal(newLevel, student.Profile.CurrentAcademicLevelId);
            Assert.Equal(AcademicStanding.Probation, student.Profile.AcademicStanding);
        }

        [Fact]
        public void UpdateAcademicProfile_rejects_empty_input_and_bad_values()
        {
            var student = NewStudent();

            Assert.Equal("STUDENT_DATA_INVALID", student.UpdateAcademicProfile(null, null, Now).Error?.Code);
            Assert.Equal("STUDENT_DATA_INVALID", student.UpdateAcademicProfile(Guid.Empty, null, Now).Error?.Code);
            Assert.Equal("STUDENT_DATA_INVALID", student.UpdateAcademicProfile(null, (AcademicStanding)99, Now).Error?.Code);
        }

        [Fact]
        public void Graduated_and_dismissed_students_cannot_be_updated()
        {
            var student = NewStudent();
            ChangeStatus(student, StudentAcademicStatus.Graduated, Today, null);

            var result = student.UpdateAcademicProfile(null, AcademicStanding.Honors, Now);

            Assert.Equal("STUDENT_NOT_ACTIVE", result.Error?.Code);
        }

        // ---------- the rules table ----------

        [Fact]
        public void Rules_table_matches_the_documented_transitions()
        {
            Assert.True(StudentStatusRules.CanTransition(StudentAcademicStatus.Active, StudentAcademicStatus.Graduated));
            Assert.True(StudentStatusRules.CanTransition(StudentAcademicStatus.OnLeave, StudentAcademicStatus.Active));
            Assert.True(StudentStatusRules.CanTransition(StudentAcademicStatus.Suspended, StudentAcademicStatus.Dismissed));
            Assert.True(StudentStatusRules.CanTransition(StudentAcademicStatus.Withdrawn, StudentAcademicStatus.Active));

            Assert.False(StudentStatusRules.CanTransition(StudentAcademicStatus.OnLeave, StudentAcademicStatus.Graduated));
            Assert.False(StudentStatusRules.CanTransition(StudentAcademicStatus.Suspended, StudentAcademicStatus.OnLeave));
            Assert.False(StudentStatusRules.CanTransition(StudentAcademicStatus.Active, StudentAcademicStatus.Active));

            foreach (var from in new[] { StudentAcademicStatus.Dismissed, StudentAcademicStatus.Graduated })
            {
                Assert.True(StudentStatusRules.IsTerminal(from));

                foreach (var to in Enum.GetValues<StudentAcademicStatus>())
                {
                    Assert.False(StudentStatusRules.CanTransition(from, to));
                }
            }

            Assert.False(StudentStatusRules.RequiresReason(StudentAcademicStatus.Graduated));
            Assert.False(StudentStatusRules.RequiresReason(StudentAcademicStatus.Active));
        }
    }
}
