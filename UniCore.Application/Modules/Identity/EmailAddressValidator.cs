using System.Net.Mail;

namespace UniCore.Application.Modules.Identity
{
    public static class EmailAddressValidator
    {
        public static bool IsValid(string? email)
        {
            var clean = email?.Trim();

            return !string.IsNullOrEmpty(clean)
                && clean.Length <= 256
                && MailAddress.TryCreate(clean, out var parsed)
                && string.Equals(parsed.Address, clean, StringComparison.Ordinal);
        }
    }


}
