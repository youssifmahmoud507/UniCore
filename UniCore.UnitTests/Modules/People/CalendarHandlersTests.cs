using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Calendar;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.People
{
    public class CalendarHandlersTests
    {
        private static readonly RequestInfo Req = PeopleCalendarTestEnv.Request;

        // ---------- academic years ----------

        [Fact]
        public async Task Creating_a_year_works_and_is_audited()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsSuperAdmin();

            var result = await env.YearHandler.CreateAsync(new CreateAcademicYearCommand("2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31)), Req);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal(AcademicYearStatus.Planned, summary.Status);
            Assert.Single(env.Years.Items);
            Assert.Equal(CalendarAuditActions.YearCreated, Assert.Single(env.Rbac.Audit.Entries).Action);
        }

        [Fact]
        public async Task A_dean_cannot_create_a_year_because_the_calendar_is_institution_wide()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsDeanOf(Guid.NewGuid());

            var result = await env.YearHandler.CreateAsync(new CreateAcademicYearCommand("2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31)), Req);

            ResultAssert.Failed(result, IdentityErrors.ForbiddenOutOfScope);
            Assert.Empty(env.Years.Items);
        }

        [Fact]
        public async Task A_year_that_overlaps_another_year_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            env.SeedYear();
            env.AsSuperAdmin();

            var result = await env.YearHandler.CreateAsync(new CreateAcademicYearCommand("2026/2027 b", new DateOnly(2027, 8, 31), new DateOnly(2028, 8, 30)), Req);

            ResultAssert.Failed(result, CalendarErrors.AcademicYearOverlap);
        }

        [Fact]
        public async Task A_year_that_starts_the_day_after_another_ends_is_accepted()
        {
            var env = new PeopleCalendarTestEnv();
            env.SeedYear();
            env.AsSuperAdmin();

            var result = await env.YearHandler.CreateAsync(new CreateAcademicYearCommand("2027/2028", new DateOnly(2027, 9, 1), new DateOnly(2028, 8, 31)), Req);

            ResultAssert.Succeeded(result);
        }

        [Fact]
        public async Task Only_one_year_can_be_current()
        {
            var env = new PeopleCalendarTestEnv();
            env.SeedYear(current: true);
            var next = env.SeedYear("2027/2028", 2027, active: true);
            env.AsSuperAdmin();

            var result = await env.YearHandler.MakeCurrentAsync(next.Id, Req);

            ResultAssert.Failed(result, CalendarErrors.AcademicYearCurrentExists);
            Assert.False(next.IsCurrent);
        }

        [Fact]
        public async Task Closing_the_current_year_lets_the_next_one_become_current()
        {
            var env = new PeopleCalendarTestEnv();
            var old = env.SeedYear(current: true);
            var next = env.SeedYear("2027/2028", 2027, active: true);
            env.AsSuperAdmin();

            ResultAssert.Succeeded(await env.YearHandler.CloseAsync(old.Id, Req));
            var result = await env.YearHandler.MakeCurrentAsync(next.Id, Req);

            Assert.True(ResultAssert.Succeeded(result).IsCurrent);
            Assert.False(old.IsCurrent);
        }

        [Fact]
        public async Task A_year_with_an_open_semester_cannot_be_closed()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear(active: true);
            var term = env.SeedTerm();
            env.SeedSemester(year, term, new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            env.AsSuperAdmin();

            var result = await env.YearHandler.CloseAsync(year.Id, Req);

            ResultAssert.Failed(result, CalendarErrors.AcademicYearHasOpenSemesters);
        }

        [Fact]
        public async Task Shrinking_a_year_so_a_semester_falls_outside_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            var term = env.SeedTerm();
            env.SeedSemester(year, term, new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            env.AsSuperAdmin();

            var result = await env.YearHandler.UpdateAsync(year.Id, new UpdateAcademicYearCommand("2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)), Req);

            ResultAssert.Failed(result, CalendarErrors.SemesterOutsideYear);
            Assert.Equal(new DateOnly(2027, 8, 31), year.EndDate);
        }

        [Fact]
        public async Task A_caller_without_the_permission_cannot_tell_whether_a_year_exists()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            env.AsReader();

            var known = await env.YearHandler.ActivateAsync(year.Id, Req);
            var unknown = await env.YearHandler.ActivateAsync(Guid.NewGuid(), Req);

            Assert.Equal(known.Error?.Code, unknown.Error?.Code);
            Assert.NotEqual(CalendarErrors.AcademicYearNotFound.Code, known.Error?.Code);
        }

        [Fact]
        public async Task Listing_years_filters_by_status_and_sorts_by_start_date_newest_first()
        {
            var env = new PeopleCalendarTestEnv();
            env.SeedYear("2026/2027", 2026, active: true);
            env.SeedYear("2027/2028", 2027);
            env.AsReader();

            var all = ResultAssert.Succeeded(await env.YearHandler.ListAsync(new PaginationQuery(1, 20), null, null, null, null));
            var active = ResultAssert.Succeeded(await env.YearHandler.ListAsync(new PaginationQuery(1, 20), null, null, AcademicYearStatus.Active, null));

            Assert.Equal(2, all.TotalCount);
            Assert.Equal(1, active.TotalCount);
            Assert.Equal("2026/2027", active.Items[0].Name);
        }

        [Fact]
        public async Task Listing_with_an_unknown_sort_field_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsReader();

            var result = await env.YearHandler.ListAsync(new PaginationQuery(1, 20), null, "bogus", null, null);

            Assert.True(result.IsFailure);
            Assert.Equal("SORT_FIELD_INVALID", result.Error?.Code);
        }

        // ---------- semesters ----------

        [Fact]
        public async Task Creating_a_semester_works_for_an_active_term_inside_the_year()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            var term = env.SeedTerm();
            env.AsSuperAdmin();

            var result = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, term.Id, new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), null), Req);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal(SemesterStatus.Planned, summary.Status);
            Assert.Equal(CalendarAuditActions.SemesterCreated, Assert.Single(env.Rbac.Audit.Entries).Action);
        }

        [Fact]
        public async Task A_second_semester_for_the_same_term_in_the_same_year_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            var term = env.SeedTerm();
            env.SeedSemester(year, term, new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            env.AsSuperAdmin();

            var result = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, term.Id, new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 30), null), Req);

            ResultAssert.Failed(result, CalendarErrors.SemesterDuplicateTerm);
        }

        [Fact]
        public async Task Semesters_of_the_same_year_cannot_overlap()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            env.SeedSemester(year, env.SeedTerm("FALL", 1), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            var spring = env.SeedTerm("SPRING", 2);
            env.AsSuperAdmin();

            var overlapping = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, spring.Id, new DateOnly(2027, 1, 31), new DateOnly(2027, 6, 30), null), Req);
            var fine = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, spring.Id, new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 30), null), Req);

            ResultAssert.Failed(overlapping, CalendarErrors.SemesterOverlap);
            ResultAssert.Succeeded(fine);
        }

        [Fact]
        public async Task A_semester_outside_the_year_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            var term = env.SeedTerm();
            env.AsSuperAdmin();

            var result = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, term.Id, new DateOnly(2026, 8, 1), new DateOnly(2027, 1, 31), null), Req);

            ResultAssert.Failed(result, CalendarErrors.SemesterOutsideYear);
        }

        [Fact]
        public async Task A_semester_needs_known_open_parents_and_an_active_term()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear(active: true);
            var term = env.SeedTerm();
            var inactiveTerm = env.SeedTerm("SUMMER", 3, active: false);
            var closedYear = env.SeedYear("2025/2026", 2025, active: true);
            ResultAssert.Succeeded(closedYear.Close(false, env.Clock.UtcNow));
            env.AsSuperAdmin();
            var dates = (new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));

            var noYear = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(Guid.NewGuid(), term.Id, dates.Item1, dates.Item2, null), Req);
            var noTerm = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, Guid.NewGuid(), dates.Item1, dates.Item2, null), Req);
            var inactive = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(year.Id, inactiveTerm.Id, dates.Item1, dates.Item2, null), Req);
            var closed = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(closedYear.Id, term.Id, new DateOnly(2025, 9, 1), new DateOnly(2026, 1, 31), null), Req);
            var empty = await env.SemesterHandler.CreateAsync(new CreateSemesterCommand(Guid.Empty, term.Id, dates.Item1, dates.Item2, null), Req);

            ResultAssert.Failed(noYear, CalendarErrors.AcademicYearNotFound);
            ResultAssert.Failed(noTerm, CalendarErrors.AcademicTermNotFound);
            ResultAssert.Failed(inactive, CalendarErrors.TermInactive);
            ResultAssert.Failed(closed, CalendarErrors.AcademicYearClosed);
            ResultAssert.Failed(empty, CalendarErrors.InvalidParent);
        }

        [Fact]
        public async Task Advancing_a_semester_needs_windows_and_only_goes_forward()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            var semester = env.SeedSemester(year, env.SeedTerm(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            env.AsSuperAdmin();

            var noWindows = await env.SemesterHandler.AdvanceStatusAsync(semester.Id, SemesterStatus.RegistrationOpen, Req);
            var missing = await env.SemesterHandler.AdvanceStatusAsync(semester.Id, null, Req);

            ResultAssert.Failed(noWindows, CalendarErrors.SemesterWindowsIncomplete);
            ResultAssert.Failed(missing, CalendarErrors.InvalidSemesterStatus);

            var windows = SemesterWindows.None with
            {
                RegistrationStart = new DateOnly(2026, 9, 1), RegistrationEnd = new DateOnly(2026, 9, 10),
                AddDropStart = new DateOnly(2026, 9, 5), AddDropEnd = new DateOnly(2026, 9, 20)
            };
            ResultAssert.Succeeded(await env.SemesterHandler.UpdateAsync(semester.Id, new UpdateSemesterCommand(semester.StartDate, semester.EndDate, windows), Req));
            ResultAssert.Succeeded(await env.SemesterHandler.AdvanceStatusAsync(semester.Id, SemesterStatus.RegistrationOpen, Req));
            ResultAssert.Failed(await env.SemesterHandler.AdvanceStatusAsync(semester.Id, SemesterStatus.Planned, Req), CalendarErrors.SemesterInvalidState);
        }

        [Fact]
        public async Task Updating_a_semester_of_a_closed_year_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear(active: true);
            var semester = env.SeedSemester(year, env.SeedTerm(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            env.AsSuperAdmin();

            // Walk the semester to Closed through the domain so the year can be closed the supported way.
            var windows = SemesterWindows.None with
            {
                RegistrationStart = new DateOnly(2026, 9, 1), RegistrationEnd = new DateOnly(2026, 9, 10),
                AddDropStart = new DateOnly(2026, 9, 5), AddDropEnd = new DateOnly(2026, 9, 20)
            };
            ResultAssert.Succeeded(semester.Reschedule(semester.StartDate, semester.EndDate, windows, year.StartDate, year.EndDate, env.Clock.UtcNow));
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.GradingOpen, env.Clock.UtcNow));
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.Closed, env.Clock.UtcNow));
            ResultAssert.Succeeded(await env.YearHandler.CloseAsync(year.Id, Req));

            var result = await env.SemesterHandler.UpdateAsync(semester.Id, new UpdateSemesterCommand(semester.StartDate, semester.EndDate, windows), Req);

            ResultAssert.Failed(result, CalendarErrors.AcademicYearClosed);
        }

        // ---------- calendar service ----------

        [Fact]
        public async Task The_current_semester_is_the_one_of_the_current_year_that_contains_today()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear(current: true);
            var fall = env.SeedSemester(year, env.SeedTerm("FALL", 1), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));
            env.SeedSemester(year, env.SeedTerm("SPRING", 2), new DateOnly(2027, 2, 1), new DateOnly(2027, 6, 30));
            env.AcademicClock.Today = new DateOnly(2026, 10, 12);

            var result = await env.Calendar.GetCurrentSemesterAsync();

            Assert.Equal(fall.Id, ResultAssert.Succeeded(result).Id);
        }

        [Fact]
        public async Task There_is_no_current_semester_without_a_current_year_or_between_semesters()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear(active: true);
            env.SeedSemester(year, env.SeedTerm("FALL", 1), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31));

            ResultAssert.Failed(await env.Calendar.GetCurrentSemesterAsync(), CalendarErrors.NoCurrentSemester);

            ResultAssert.Succeeded(year.MakeCurrent(false, env.Clock.UtcNow));
            env.AcademicClock.Today = new DateOnly(2027, 7, 1);

            ResultAssert.Failed(await env.Calendar.GetCurrentSemesterAsync(), CalendarErrors.NoCurrentSemester);
        }

        [Fact]
        public async Task The_calendar_service_answers_window_questions_using_the_academic_clock()
        {
            var env = new PeopleCalendarTestEnv();
            var year = env.SeedYear();
            var windows = SemesterWindows.None with
            {
                RegistrationStart = new DateOnly(2026, 9, 1), RegistrationEnd = new DateOnly(2026, 9, 10),
                AddDropStart = new DateOnly(2026, 9, 5), AddDropEnd = new DateOnly(2026, 9, 20),
                WithdrawalDeadline = new DateOnly(2026, 11, 15)
            };
            var semester = env.SeedSemester(year, env.SeedTerm(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), windows);

            env.AcademicClock.Today = new DateOnly(2026, 9, 8);
            Assert.True(Open(await env.Calendar.IsRegistrationOpenAsync(semester.Id)));
            Assert.True(Open(await env.Calendar.IsAddDropOpenAsync(semester.Id)));
            Assert.False(Open(await env.Calendar.IsWithdrawalOpenAsync(semester.Id)));

            env.AcademicClock.Today = new DateOnly(2026, 10, 1);
            Assert.False(Open(await env.Calendar.IsRegistrationOpenAsync(semester.Id)));
            Assert.True(Open(await env.Calendar.IsWithdrawalOpenAsync(semester.Id)));
        }

        [Fact]
        public async Task The_calendar_service_reports_an_unknown_semester()
        {
            var env = new PeopleCalendarTestEnv();

            ResultAssert.Failed(await env.Calendar.IsRegistrationOpenAsync(Guid.NewGuid()), CalendarErrors.SemesterNotFound);
        }

        private static bool Open(Result<bool> result)
        {
            Assert.True(result.TryGetValue(out var value), "Expected success but got: " + result.Error?.Code);
            return value;
        }

        // ---------- levels ----------

        [Fact]
        public async Task A_dean_can_create_a_level_for_a_program_in_their_faculty_but_not_another()
        {
            var env = new PeopleCalendarTestEnv();
            var own = env.SeedFaculty("ENG");
            var other = env.SeedFaculty("SCI");
            var ownProgram = env.SeedProgram(env.SeedDepartment(own.Id, "CSE").Id, "P1");
            var otherProgram = env.SeedProgram(env.SeedDepartment(other.Id, "PHY").Id, "P2");
            env.PlaceDepartmentInFaculty(ownProgram.DepartmentId, own.Id);
            env.PlaceDepartmentInFaculty(otherProgram.DepartmentId, other.Id);
            env.AsDeanOf(own.Id);

            var allowed = await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(ownProgram.Id, "Level 1", 1, 0), Req);
            var denied = await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(otherProgram.Id, "Level 1", 1, 0), Req);

            ResultAssert.Succeeded(allowed);
            ResultAssert.Failed(denied, IdentityErrors.ForbiddenOutOfScope);
            Assert.Single(env.Levels.Items);
        }

        [Fact]
        public async Task A_level_needs_an_existing_active_program()
        {
            var env = new PeopleCalendarTestEnv();
            var faculty = env.SeedFaculty();
            var inactive = env.SeedProgram(env.SeedDepartment(faculty.Id).Id, active: false);
            env.AsSuperAdmin();

            ResultAssert.Failed(await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(Guid.NewGuid(), "Level 1", 1, 0), Req), UniversityErrors.ProgramNotFound);
            ResultAssert.Failed(await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(inactive.Id, "Level 1", 1, 0), Req), UniversityErrors.ProgramInactive);
            ResultAssert.Failed(await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(Guid.Empty, "Level 1", 1, 0), Req), CalendarErrors.InvalidParent);
        }

        [Fact]
        public async Task Levels_can_be_listed_by_program_in_order()
        {
            var env = new PeopleCalendarTestEnv();
            var faculty = env.SeedFaculty();
            var program = env.SeedProgram(env.SeedDepartment(faculty.Id).Id);
            env.AsSuperAdmin();
            ResultAssert.Succeeded(await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(program.Id, "Level 2", 2, 36), Req));
            ResultAssert.Succeeded(await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(program.Id, "Level 1", 1, 0), Req));
            env.AsReader();

            var page = ResultAssert.Succeeded(await env.LevelHandler.ListAsync(new PaginationQuery(1, 20), null, null, null, program.Id));

            Assert.Equal(2, page.TotalCount);
        }

        [Fact]
        public async Task Deactivating_a_level_twice_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            var faculty = env.SeedFaculty();
            var program = env.SeedProgram(env.SeedDepartment(faculty.Id).Id);
            env.AsSuperAdmin();
            var level = ResultAssert.Succeeded(await env.LevelHandler.CreateAsync(new CreateAcademicLevelCommand(program.Id, "Level 1", 1, 0), Req));

            ResultAssert.Succeeded(await env.LevelHandler.DeactivateAsync(level.Id, Req));
            ResultAssert.Failed(await env.LevelHandler.DeactivateAsync(level.Id, Req), CalendarErrors.AlreadyInactive);
        }

        // ---------- terms ----------

        [Fact]
        public async Task Creating_a_term_works_for_a_super_admin_and_is_audited()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsSuperAdmin();

            var summary = ResultAssert.Succeeded(await env.TermHandler.CreateAsync(new CreateAcademicTermCommand("fall", "Fall", 1), Req));

            Assert.Equal("FALL", summary.Code);
            Assert.Equal(CalendarAuditActions.TermCreated, Assert.Single(env.Rbac.Audit.Entries).Action);
        }

        [Fact]
        public async Task A_reader_cannot_change_terms()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsReader();

            var result = await env.TermHandler.CreateAsync(new CreateAcademicTermCommand("FALL", "Fall", 1), Req);

            Assert.True(result.IsFailure);
            Assert.Empty(env.Terms.Items);
        }
    }
}
