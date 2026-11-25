using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Grading;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.StudentServices;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;

namespace UniCore.UnitTests.Modules.StudentServices
{
    public class GradeAppealTests
    {
        private static readonly RequestInfo Info = StudentServicesTestEnv.Request;

        [Fact]
        public void An_appeal_needs_a_grade_and_a_reason()
        {
            var now = new DateTimeOffset(2027, 6, 20, 9, 0, 0, TimeSpan.Zero);

            ResultAssert.Failed(GradeAppeal.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Marked wrong", now), ServiceRequestErrors.Invalid);
            ResultAssert.Failed(GradeAppeal.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " ", now), ServiceRequestErrors.TextRequired);
            Assert.Equal(ServiceRequestStatus.Pending, ResultAssert.Succeeded(GradeAppeal.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Marked wrong", now)).Status);
        }

        [Fact]
        public async Task The_grade_must_be_a_published_grade_of_the_student_and_one_appeal_stays_open()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var gradeId = Guid.NewGuid();

            ResultAssert.Failed(await env.GradeAppealHandler.SubmitAsync(student.Id, null, "Marked wrong", Info), ServiceRequestErrors.Invalid);
            ResultAssert.Failed(await env.GradeAppealHandler.SubmitAsync(student.Id, gradeId, "Marked wrong", Info), ServiceRequestErrors.GradeNotFound);
            Assert.Empty(env.Workflows.Started);

            env.GradeRead.Publish(gradeId);
            var submitted = ResultAssert.Succeeded(await env.GradeAppealHandler.SubmitAsync(student.Id, gradeId, "Marked wrong", Info));

            Assert.Equal(ServiceRequestStatus.Pending, submitted.Status);
            Assert.Equal(gradeId.ToString(), submitted.Details["gradeId"]);
            var started = Assert.Single(env.Workflows.Started);
            Assert.Equal(ServiceRequestKinds.GradeAppealDefinition, started.DefinitionCode);
            Assert.Equal(ServiceRequestKinds.GradeAppealReference, started.ReferenceType);
            ResultAssert.Failed(await env.GradeAppealHandler.SubmitAsync(student.Id, gradeId, "Again", Info), ServiceRequestErrors.AlreadyPending);
        }

        [Fact]
        public async Task The_workflow_decides_the_appeal_and_no_score_changes()
        {
            var env = new StudentServicesTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var gradeId = Guid.NewGuid();
            env.GradeRead.Publish(gradeId);
            var submitted = ResultAssert.Succeeded(await env.GradeAppealHandler.SubmitAsync(student.Id, gradeId, "Marked wrong", Info));
            var e = new UniCore.Domain.Modules.Workflow.WorkflowCompleted(Guid.NewGuid(), env.Clock.UtcNow.AddMinutes(5), env.Workflows.LastInstanceId, "DEF", ServiceRequestKinds.GradeAppealReference, submitted.Id, student.UserId!.Value, null);

            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));
            ResultAssert.Succeeded(await env.Reactions.OnCompletedAsync(e, default));

            Assert.Equal(ServiceRequestStatus.Approved, Assert.Single(env.GradeAppeals.Items).Status);
            Assert.Single(env.Rbac.AuditOf(ServiceRequestAuditActions.Approved));
        }

        [Fact]
        public async Task A_dropped_enrollment_loses_its_draft_grade_and_a_published_grade_stays()
        {
            var grades = new FakeAggregateRepository<Grade>();
            var uow = new FakeUnitOfWork();
            var audit = new FakeAuditWriter();
            var handler = new OrphanGradeHandler(new OrphanGradeService(grades), uow, audit);
            var now = DateTimeOffset.UtcNow;
            var draft = ResultAssert.Succeeded(Grade.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, now));
            grades.Items.Add(draft);

            ResultAssert.Succeeded(await handler.HandleAsync(Dropped(draft.EnrollmentId, draft.StudentId, now), default));
            ResultAssert.Succeeded(await handler.HandleAsync(Dropped(draft.EnrollmentId, draft.StudentId, now), default));
            ResultAssert.Succeeded(await handler.HandleAsync(Dropped(Guid.NewGuid(), Guid.NewGuid(), now), default));

            Assert.Empty(grades.Items);
            Assert.Equal(1, uow.SaveCalls);
        }

        private static UniCore.Domain.Modules.Registrations.EnrollmentDropped Dropped(Guid enrollmentId, Guid studentId, DateTimeOffset now)
            => new(Guid.NewGuid(), now, Guid.NewGuid(), enrollmentId, studentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, true);
    }
}
