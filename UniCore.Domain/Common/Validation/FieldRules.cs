namespace UniCore.Domain.Common.Validation
{
    // Shared length limits and small yes/no checks for text fields.
    // These only answer true or false. Each entity still decides which error to return, in its own factory.
    public static class FieldRules
    {
        public const int CodeMinLength = 2;
        public const int CodeMaxLength = 20;
        public const int NameMinLength = 2;
        public const int NameMaxLength = 200;
        public const int DescriptionMaxLength = 1000;
        public const int EmailMaxLength = 254;
        public const int PhoneMaxLength = 20;
        public const int WebsiteMaxLength = 200;
        public const int TimeZoneMaxLength = 64;

        // Optional text: blank becomes null, otherwise the trimmed text.
        public static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        // Codes are short identifiers: ASCII letters, digits, "-" and "_". The caller upper-cases them first.
        public static bool IsValidCode(string? value, int minLength, int maxLength)
        {
            if (value is null || value.Length < minLength || value.Length > maxLength)
            {
                return false;
            }

            foreach (var c in value)
            {
                var isLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                var isDigit = c >= '0' && c <= '9';

                if (!isLetter && !isDigit && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        // Deliberately simple: one "@", something before it, a dot inside the domain part, no spaces.
        public static bool IsValidEmail(string? value)
        {
            if (value is null || value.Length > EmailMaxLength)
            {
                return false;
            }

            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    return false;
                }
            }

            var at = value.IndexOf('@');

            if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
            {
                return false;
            }

            var domain = value[(at + 1)..];

            return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
        }

        // Digits plus an optional leading "+", spaces, "-", "(" and ")". At least 5 digits.
        public static bool IsValidPhone(string? value)
        {
            if (value is null || value.Length > PhoneMaxLength)
            {
                return false;
            }

            var digits = 0;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c >= '0' && c <= '9')
                {
                    digits++;
                    continue;
                }

                if (c == '+' && i == 0)
                {
                    continue;
                }

                if (c == ' ' || c == '-' || c == '(' || c == ')')
                {
                    continue;
                }

                return false;
            }

            return digits >= 5;
        }

        // An absolute http or https address with a host.
        public static bool IsValidWebsite(string? value)
        {
            if (value is null || value.Length > WebsiteMaxLength)
            {
                return false;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return false;
            }

            var isHttp = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

            return isHttp && !string.IsNullOrEmpty(uri.Host);
        }

        // Format only (for example "Africa/Cairo" or "UTC"). Checking that the id really exists on the server
        // depends on the machine's time zone data, so that check belongs to the application layer.
        public static bool IsValidTimeZoneId(string? value)
        {
            if (value is null || value.Length == 0 || value.Length > TimeZoneMaxLength)
            {
                return false;
            }

            foreach (var c in value)
            {
                var isLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                var isDigit = c >= '0' && c <= '9';

                if (!isLetter && !isDigit && c != '/' && c != '_' && c != '-' && c != '+')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
