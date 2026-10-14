using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Outbox;
using UniCore.Domain.Modules.Students;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Outbox
{
    [Collection(ApiCollection.Name)]
    public sealed class StudentAdmittedPipelineTests(ApiFixture fixture)
    {
        private static readonly string EventType = OutboxMessageSerializer.TypeNameOf(typeof(StudentAdmitted));

        private readonly TestApi _api = fixture.Api;

        private async Task<List<OutboxMessage>> MessagesForAsync(Guid studentId)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = studentId.ToString();

            return await db.Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.Type == EventType && m.Payload.Contains(id))
                .ToListAsync();
        }

        private async Task<int> EventCountAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await db.Set<OutboxMessage>().CountAsync(m => m.Type == EventType);
        }

        private async Task<List<AuditLog>> AuditRowsAsync(Guid studentId)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = studentId.ToString();

            return await db.Set<AuditLog>().AsNoTracking()
                .Where(a => a.Action == AuditActions.StudentAdmitted && a.EntityId == id)
                .ToListAsync();
        }

        private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow + timeout;

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await condition())
                {
                    return true;
                }

                await Task.Delay(200);
            }

            return await condition();
        }

        [Fact]
        public async Task Admitting_a_student_delivers_StudentAdmitted_and_audits_it_once()
        {
            var spec = new AdmitSpec();
            var admin = await _api.AdminTokenAsync();

            var created = await _api.Client.PostAsync("/api/v1/students", spec.Body, admin);

            Assert.Equal(201, created.Code);
            var studentId = Guid.Parse(created.Prop("student").GetProperty("summary").GetProperty("id").GetString() ?? string.Empty);

            // Saved with the student, in the same transaction.
            var message = Assert.Single(await MessagesForAsync(studentId));
            Assert.Contains(spec.Number(1), message.Payload);

            var delivered = await WaitUntilAsync(
                async () => (await MessagesForAsync(studentId)).All(m => m.Status == OutboxMessageStatus.Processed),
                TimeSpan.FromSeconds(30));

            Assert.True(delivered);

            var audit = Assert.Single(await AuditRowsAsync(studentId));
            Assert.Null(audit.ActorUserId);
            Assert.Equal("Student", audit.EntityName);
            Assert.Equal(spec.Number(1), audit.NewValues);
        }

        [Fact]
        public async Task A_rejected_admission_leaves_nothing_in_the_outbox()
        {
            var spec = new AdmitSpec();
            var admin = await _api.AdminTokenAsync();
            var before = await EventCountAsync();

            var rejected = await _api.Client.PostAsync("/api/v1/students", new
            {
                personProfileId = spec.Person,
                admissionDate = spec.Admission,
                academicProgramId = Guid.Empty,
                programCode = spec.Code,
                studyPlanVersionId = spec.Plan,
                admissionAcademicYearId = spec.AcademicYear,
                admissionYear = spec.Admission.Year,
                createAccount = false
            }, admin);

            Assert.Equal(400, rejected.Code);
            Assert.Equal(before, await EventCountAsync());
        }
    }
}
