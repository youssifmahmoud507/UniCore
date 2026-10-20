using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The events reach the notification handlers through the outbox. Each test processes what is due and looks again
    // until what it expects is there (the API's own background job may also process the same messages).
    [Collection(ApiCollection.Name)]
    public sealed class NotificationsApiTests(ApiFixture fixture)
    {
        private const string ActionRequired = "WORKFLOW_ACTION_REQUIRED";
        private const string StatusChanged = "REQUEST_STATUS_CHANGED";
        private const string PaymentReceived = "PAYMENT_RECEIVED";

        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private OutboxProcessor Processor => fixture.Services.GetRequiredService<OutboxProcessor>();

        private static string NewCode() => $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        private async Task<string> TokenAsync(string prefix, string role, ScopeType? scopeType = null, Guid? scopeId = null)
        {
            var user = await _api.CreateUserAsync(prefix, role, scopeType, scopeId);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }

        private async Task<bool> EventuallyAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);

            while (DateTime.UtcNow < deadline)
            {
                await Processor.ProcessBatchAsync(200);

                if (await condition())
                {
                    return true;
                }

                await Task.Delay(300);
            }

            return await condition();
        }

        private async Task<List<JsonElement>> MineAsync(string token)
        {
            var response = await Client.GetAsync("/api/v1/me/notifications?pageSize=100", token);
            Assert.Equal(200, response.Code);

            return [.. response.Prop("items").EnumerateArray()];
        }

        private static bool About(JsonElement notification, string type, Guid reference)
            => notification.GetProperty("type").GetString() == type
                && notification.GetProperty("referenceId").ValueKind != JsonValueKind.Null
                && notification.GetProperty("referenceId").GetGuid() == reference;

        private async Task<bool> HasAsync(string token, string type, Guid reference)
            => (await MineAsync(token)).Any(n => About(n, type, reference));

        private Task<ApiResponse> StartAsync(string token, string code, Guid department)
            => Client.PostAsync("/api/v1/workflows", new { definitionCode = code, referenceType = "TestRequest", referenceId = Guid.NewGuid(), scopeType = "Department", scopeId = department }, token);

        private Task<ApiResponse> ActAsync(string token, Guid id, string action, string? comment = null)
            => Client.PostAsync($"/api/v1/workflows/{id}/{action}", new { comment }, token);

        [Fact]
        public async Task The_workflow_events_tell_the_approvers_and_the_requester()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("ntfs", RoleNames.Student);
            var head = await TokenAsync("ntfh", RoleNames.DepartmentHead, ScopeType.Department, department);
            var otherHead = await TokenAsync("ntfo", RoleNames.DepartmentHead, ScopeType.Department, Guid.NewGuid());
            var dean = await TokenAsync("ntfd", RoleNames.Dean, ScopeType.Global);
            var registrar = await TokenAsync("ntfr", RoleNames.Registrar, ScopeType.Global);

            var started = await StartAsync(student, "WITHDRAWAL_REQUEST", department);
            Assert.Equal(201, started.Code);
            var id = started.GuidOf("id");

            Assert.True(await EventuallyAsync(() => HasAsync(head, ActionRequired, id)));

            Assert.Equal(200, (await ActAsync(head, id, "approve")).Code);
            Assert.True(await EventuallyAsync(() => HasAsync(dean, ActionRequired, id)));
            Assert.True(await EventuallyAsync(() => HasAsync(student, StatusChanged, id)));

            Assert.Equal(200, (await ActAsync(dean, id, "approve")).Code);
            Assert.True(await EventuallyAsync(() => HasAsync(registrar, ActionRequired, id)));

            Assert.Equal(200, (await ActAsync(registrar, id, "approve")).Code);
            Assert.True(await EventuallyAsync(async () => (await MineAsync(student)).Any(n => About(n, StatusChanged, id) && n.GetProperty("title").GetString() == "Your request was approved")));

            // The requester is never asked to approve their own request, and a head of another department is not told.
            Assert.False(await HasAsync(student, ActionRequired, id));
            Assert.False(await HasAsync(otherHead, ActionRequired, id));
        }

        [Fact]
        public async Task A_rejection_tells_the_requester_why()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("ntfs", RoleNames.Student);
            var head = await TokenAsync("ntfh", RoleNames.DepartmentHead, ScopeType.Department, department);

            var id = (await StartAsync(student, "COMPLAINT", department)).GuidOf("id");
            Assert.True(await EventuallyAsync(() => HasAsync(head, ActionRequired, id)));

            Assert.Equal(200, (await ActAsync(head, id, "reject", "no evidence")).Code);

            Assert.True(await EventuallyAsync(async () => (await MineAsync(student)).Any(n => About(n, StatusChanged, id) && n.GetProperty("content").GetString()!.Contains("no evidence"))));
        }

        [Fact]
        public async Task A_user_reads_counts_and_marks_only_their_own_notifications()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("ntfs", RoleNames.Student);
            var head = await TokenAsync("ntfh", RoleNames.DepartmentHead, ScopeType.Department, department);

            var id = (await StartAsync(student, "LEAVE_REQUEST", department)).GuidOf("id");
            Assert.True(await EventuallyAsync(() => HasAsync(head, ActionRequired, id)));

            var unread = await Client.GetAsync("/api/v1/me/notifications?unreadOnly=true&pageSize=100", head);
            var item = unread.Prop("items").EnumerateArray().First(n => About(n, ActionRequired, id));
            var notificationId = item.GetProperty("id").GetGuid();
            Assert.False(item.GetProperty("isRead").GetBoolean());

            var before = (await Client.GetAsync("/api/v1/me/notifications/unread-count", head)).Prop("count").GetInt32();
            Assert.True(before >= 1);

            // Someone else's notification looks like one that does not exist.
            var denied = await Client.PostAsync($"/api/v1/me/notifications/{notificationId}/read", null, student);
            Assert.Equal(404, denied.Code);
            Assert.Equal("NOTIFICATION_NOT_FOUND", denied.ErrorCode);
            Assert.DoesNotContain(await MineAsync(student), n => n.GetProperty("id").GetGuid() == notificationId);

            var read = await Client.PostAsync($"/api/v1/me/notifications/{notificationId}/read", null, head);
            Assert.Equal(200, read.Code);
            Assert.True(read.Prop("isRead").GetBoolean());
            Assert.Equal(before - 1, (await Client.GetAsync("/api/v1/me/notifications/unread-count", head)).Prop("count").GetInt32());

            var all = await Client.PostAsync("/api/v1/me/notifications/read-all", null, head);
            Assert.Equal(200, all.Code);
            Assert.Equal(0, (await Client.GetAsync("/api/v1/me/notifications/unread-count", head)).Prop("count").GetInt32());
        }

        [Fact]
        public async Task The_preferences_decide_what_a_user_gets_and_a_mandatory_type_cannot_be_turned_off()
        {
            var department = Guid.NewGuid();
            var student = await TokenAsync("ntfs", RoleNames.Student);
            var head = await TokenAsync("ntfh", RoleNames.DepartmentHead, ScopeType.Department, department);
            var dean = await TokenAsync("ntfd", RoleNames.Dean, ScopeType.Global);

            var defaults = await Client.GetAsync("/api/v1/me/notifications/preferences", student);
            Assert.Equal(200, defaults.Code);
            Assert.All(defaults.Body.EnumerateArray(), p => Assert.True(p.GetProperty("inAppEnabled").GetBoolean() && p.GetProperty("emailEnabled").GetBoolean()));

            var off = await Client.PutAsync($"/api/v1/me/notifications/preferences/{StatusChanged}", new { inAppEnabled = false, emailEnabled = false }, student);
            Assert.Equal(200, off.Code);
            Assert.False(off.Prop("inAppEnabled").GetBoolean());

            var shown = await Client.GetAsync("/api/v1/me/notifications/preferences", student);
            var status = shown.Body.EnumerateArray().Single(p => p.GetProperty("type").GetString() == StatusChanged);
            Assert.False(status.GetProperty("inAppEnabled").GetBoolean());
            Assert.False(status.GetProperty("emailEnabled").GetBoolean());

            var id = (await StartAsync(student, "LEAVE_REQUEST", department)).GuidOf("id");
            Assert.True(await EventuallyAsync(() => HasAsync(head, ActionRequired, id)));
            Assert.Equal(200, (await ActAsync(head, id, "approve")).Code);

            // The step after the head is the dean's: when the dean is told, the same event has been handled for the requester too.
            Assert.True(await EventuallyAsync(() => HasAsync(dean, ActionRequired, id)));
            await Processor.ProcessBatchAsync(200);
            Assert.False(await HasAsync(student, StatusChanged, id));

            var mandatory = await Client.PutAsync("/api/v1/me/notifications/preferences/FINANCIAL_HOLD_PLACED", new { inAppEnabled = false, emailEnabled = false }, student);
            Assert.Equal(422, mandatory.Code);
            Assert.Equal("NOTIFICATION_PREFERENCE_MANDATORY", mandatory.ErrorCode);

            var unknown = await Client.PutAsync("/api/v1/me/notifications/preferences/NOT_A_TYPE", new { inAppEnabled = true, emailEnabled = true }, student);
            Assert.Equal(400, unknown.Code);
            Assert.Equal("NOTIFICATION_PREFERENCE_INVALID", unknown.ErrorCode);
        }

        [Fact]
        public async Task A_payment_tells_the_student()
        {
            var admin = await _api.AdminTokenAsync();
            var email = $"{TestApi.NewName("ntfp")}@uni.test";
            var spec = new AdmitSpec(null, email, createAccount: true);

            var admitted = await Client.PostAsync("/api/v1/students", spec.Body, admin);
            Assert.Equal(201, admitted.Code);
            var studentId = admitted.Prop("student").GetProperty("summary").GetProperty("id").GetGuid();

            await _api.SetPasswordAsync(email, TestApi.DefaultPassword);
            var token = await _api.LoginTokenAsync(spec.Number(1), TestApi.DefaultPassword);

            var semester = Guid.NewGuid();

            var structure = await Client.PostAsync("/api/v1/fee-structures", new { academicProgramId = spec.Program, academicSemesterId = semester, name = "Notify" }, admin);
            Assert.Equal(201, structure.Code);
            var structureId = structure.GuidOf("id");

            var definition = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code = NewCode(), name = "Fee", description = "x", defaultAmount = 500m }, admin);
            Assert.Equal(201, definition.Code);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = definition.GuidOf("id"), amount = (decimal?)null }, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/fee-structures/{structureId}/activate", null, admin)).Code);
            Assert.Equal(200, (await Client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = spec.Program, dueDate = AdmitSpec.Today.AddDays(30) }, admin)).Code);

            var invoice = await Client.PostAsync("/api/v1/invoices", new { studentId, academicSemesterId = semester }, admin);
            Assert.Equal(201, invoice.Code);
            var invoiceId = invoice.GuidOf("id");

            var paid = await Client.PostAsync(
                $"/api/v1/invoices/{invoiceId}/payments",
                new { amount = 100m, method = "Cash" },
                admin,
                new Dictionary<string, string> { ["Idempotency-Key"] = $"ntf-{Guid.NewGuid():N}" });
            Assert.Equal(201, paid.Code);

            Assert.True(await EventuallyAsync(async () => (await MineAsync(token)).Any(n => About(n, PaymentReceived, invoiceId) && n.GetProperty("content").GetString()!.Contains("100.00"))));
        }

        [Fact]
        public async Task An_anonymous_caller_is_turned_away()
        {
            Assert.Equal(401, (await Client.GetAsync("/api/v1/me/notifications")).Code);
            Assert.Equal(401, (await Client.GetAsync("/api/v1/me/notifications/unread-count")).Code);
            Assert.Equal(401, (await Client.PostAsync("/api/v1/me/notifications/read-all")).Code);
        }
    }


}
