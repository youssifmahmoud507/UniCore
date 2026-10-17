using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniCore.IntegrationTests.Infrastructure
{
    // Builds the chain for a test: admitted student, active fee structure, generated charges, and (IssueAsync) the invoice.
    public static class InvoiceScenario
    {
        public sealed record Prepared(Guid StudentId, Guid Semester, AdmitSpec Spec, decimal Total);

        public sealed record Issued(Guid InvoiceId, Guid StudentId, Guid Semester, AdmitSpec Spec, decimal Total);

        // Everything except the invoice itself, so a test can award scholarships first.
        public static async Task<Prepared> PrepareAsync(TestApi api, string admin, params decimal[] amounts)
        {
            var client = api.Client;
            var spec = new AdmitSpec();
            var semester = Guid.NewGuid();

            var admitted = await client.PostAsync("/api/v1/students", spec.Body, admin);
            Assert.Equal(201, admitted.Code);
            var studentId = admitted.Prop("student").GetProperty("summary").GetProperty("id").GetGuid();

            var structure = await client.PostAsync("/api/v1/fee-structures", new { academicProgramId = spec.Program, academicSemesterId = semester, name = "Scenario" }, admin);
            Assert.Equal(201, structure.Code);
            var structureId = structure.GuidOf("id");

            foreach (var amount in amounts)
            {
                var code = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant();
                var definition = await client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code, name = "Fee", description = "x", defaultAmount = amount }, admin);
                Assert.Equal(201, definition.Code);

                var added = await client.PostAsync($"/api/v1/fee-structures/{structureId}/items", new { feeDefinitionId = definition.GuidOf("id"), amount = (decimal?)null }, admin);
                Assert.Equal(200, added.Code);
            }

            Assert.Equal(200, (await client.PostAsync($"/api/v1/fee-structures/{structureId}/activate", null, admin)).Code);

            var generated = await client.PostAsync("/api/v1/student-charges/generate", new { academicSemesterId = semester, academicProgramId = spec.Program, dueDate = AdmitSpec.Today.AddDays(30) }, admin);
            Assert.Equal(200, generated.Code);

            return new Prepared(studentId, semester, spec, amounts.Sum());
        }

        public static async Task<Issued> IssueAsync(TestApi api, string admin, params decimal[] amounts)
        {
            var prepared = await PrepareAsync(api, admin, amounts);

            var invoice = await api.Client.PostAsync("/api/v1/invoices", new { studentId = prepared.StudentId, academicSemesterId = prepared.Semester }, admin);
            Assert.Equal(201, invoice.Code);

            return new Issued(invoice.GuidOf("id"), prepared.StudentId, prepared.Semester, prepared.Spec, prepared.Total);
        }
    }
}
