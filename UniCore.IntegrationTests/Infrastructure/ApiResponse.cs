using System.Net;
using System.Text.Json;

namespace UniCore.IntegrationTests.Infrastructure
{
    public sealed record ApiResponse(HttpStatusCode Status, JsonElement Body, string Raw)
    {
        public int Code => (int)Status;

        public string? ErrorCode => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;

        public JsonElement Prop(string name) => Body.GetProperty(name);

        public string Str(string name) => Body.GetProperty(name).GetString() ?? string.Empty;

        public Guid GuidOf(string name) => Guid.Parse(Str(name));
    }

}
