namespace UniCore.Api.Contracts.University
{
    public sealed record AddressRequest(string? Street, string? City, string? State, string? PostalCode, string? Country);
}
