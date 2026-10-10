namespace UniCore.Api.Contracts.Auth
{
    // Nullable on purpose: missing fields are reported by our handlers as proper Result errors.
    public sealed record LoginRequest(string? Login, string? Password);

}
