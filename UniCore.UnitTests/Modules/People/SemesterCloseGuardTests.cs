using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Calendar;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.People
{
    public class SemesterCloseGuardTests
    {
        private static readonly RequestInfo Req = new("127.0.0.1", "tests");
        private static readonly Error Blocked = Error.BusinessRule("TEST_BLOCKED", "Blocked by a guard.");

        private sealed class FakeGuard(Result result) : ISemesterCloseGuard
        {
            public int Calls { get; private set; }

            public Task<Result> CanCloseAsync(Guid semesterId, CancellationToken cancellationToken = default)
            {
                Calls++;
                return Task.FromResult(result);
            }
        }

        private static (PeopleCalendarTestEnv Env, AcademicSemester Semester) GradingOpenSemester()
        {
            var env = new PeopleCalendarTestEnv();
            var windows = SemesterWindows.None with
            {
                RegistrationStart = new DateOnly(2026, 9, 1), RegistrationEnd = new DateOnly(2026, 9, 10),
                AddDropStart = new DateOnly(2026, 9, 5), AddDropEnd = new DateOnly(2026, 9, 20)
            };
            var semester = env.SeedSemester(env.SeedYear(), env.SeedTerm(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), windows);

            foreach (var status in new[] { SemesterStatus.RegistrationOpen, SemesterStatus.InProgress, SemesterStatus.Examinations, SemesterStatus.GradingOpen })
            {
                ResultAssert.Succeeded(semester.AdvanceTo(status, env.Clock.UtcNow));
            }

            env.AsSuperAdmin();
            return (env, semester);
        }

        [Fact]
        public async Task A_guard_that_fails_stops_the_semester_from_closing()
        {
            var (env, semester) = GradingOpenSemester();
            var guard = new FakeGuard(Result.Fail(Blocked));
            env.CloseGuards.Add(guard);

            var result = await env.SemesterHandler.AdvanceStatusAsync(semester.Id, SemesterStatus.Closed, Req);

            ResultAssert.Failed(result, Blocked);
            Assert.Equal(SemesterStatus.GradingOpen, semester.Status);
            Assert.Equal(1, guard.Calls);
        }

        [Fact]
        public async Task Closing_works_when_every_guard_agrees_and_guards_are_not_asked_about_other_steps()
        {
            var (env, semester) = GradingOpenSemester();
            var guard = new FakeGuard(Result.Ok());
            env.CloseGuards.Add(guard);

            var result = await env.SemesterHandler.AdvanceStatusAsync(semester.Id, SemesterStatus.Closed, Req);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(SemesterStatus.Closed, semester.Status);
            Assert.Equal(1, guard.Calls);
        }

        [Fact]
        public async Task Guards_are_not_asked_when_the_semester_moves_to_a_status_other_than_closed()
        {
            var env = new PeopleCalendarTestEnv();
            var windows = SemesterWindows.None with
            {
                RegistrationStart = new DateOnly(2026, 9, 1), RegistrationEnd = new DateOnly(2026, 9, 10),
                AddDropStart = new DateOnly(2026, 9, 5), AddDropEnd = new DateOnly(2026, 9, 20)
            };
            var semester = env.SeedSemester(env.SeedYear(), env.SeedTerm(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), windows);
            var guard = new FakeGuard(Result.Fail(Blocked));
            env.CloseGuards.Add(guard);
            env.AsSuperAdmin();

            var result = await env.SemesterHandler.AdvanceStatusAsync(semester.Id, SemesterStatus.RegistrationOpen, Req);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(0, guard.Calls);
        }
    }
}
