namespace UniCore.Application.Modules.University
{
    // Raw address fields as they arrive from the API. The Address value object validates them.
    public sealed record AddressInput(string? Street, string? City, string? State, string? PostalCode, string? Country);
}
