using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Common.Results;
using UniCore.Application.Modules.Examination;

namespace UniCore.UnitTests.Modules.Examination
{
    // N-36: sessions are university-wide, so a department head's Manage does not create or close them; exam lists without a section
    // are for whole-university managers only.
    public class ExamSessionScopeTests
    {
        private static readonly UniCore.Application.Common.Abstractions.RequestInfo Request = ExaminationTestEnv.Request;

        private static CreateExamSessionCommand Command(ExaminationTestEnv env)
            => new(env.Semester.Id, "Finals Fall 2026", ExamType.Final, new DateOnly(2027, 1, 8), new DateOnly(2027, 1, 12));

        [Fact]
        public async Task A_department_head_cannot_create_a_session_but_the_registrar_can()
        {
            var env = new ExaminationTestEnv();

            env.AsHead();
            var denied = await env.SessionHandler.CreateAsync(Command(env), Request);
            Assert.Equal(ErrorType.Forbidden, denied.Error?.ErrorType);
            Assert.Empty(env.Sessions.Items);

            env.AsRegistrar();
            Assert.True((await env.SessionHandler.CreateAsync(Command(env), Request)).IsSuccess);
        }

        [Fact]
        public async Task A_department_head_cannot_close_a_session()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();

            env.AsHead();
            var denied = await env.SessionHandler.CloseAsync(session.Id, Request);

            Assert.Equal(ErrorType.Forbidden, denied.Error?.ErrorType);
            Assert.Equal(ExamSessionStatus.Open, env.Sessions.Items.Single().Status);
        }

        [Fact]
        public async Task An_exam_list_without_a_section_is_for_whole_university_managers_only()
        {
            var env = new ExaminationTestEnv();

            env.AsHead();
            Assert.True((await env.Handler.ListAsync(new(1, 10), null, null, null)).IsFailure);

            env.AsRegistrar();
            Assert.True((await env.Handler.ListAsync(new(1, 10), null, null, null)).IsSuccess);
        }
    }
}
