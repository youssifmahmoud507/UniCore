namespace UniCore.Application.Modules.Documents
{
    public static class DocumentFileName
    {
        public const int MaxLength = 150;

        private static readonly char[] Forbidden = ['<', '>', ':', '"', '|', '?', '*', '\\', '/'];

        // Keeps only the file name part: no folders, no control or special characters, a shortened name with its
        // extension kept. Null when nothing usable is left or there is no extension.
        public static string? Sanitize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var name = raw.Trim();
            var slash = name.LastIndexOfAny(['/', '\\']);

            if (slash >= 0)
            {
                name = name[(slash + 1)..];
            }

            var cleaned = new string([.. name.Where(c => !char.IsControl(c) && Array.IndexOf(Forbidden, c) < 0)]).Trim().TrimEnd('.');
            var extension = Path.GetExtension(cleaned);

            if (cleaned.Length == 0 || string.IsNullOrEmpty(extension))
            {
                return null;
            }

            if (cleaned.Length > MaxLength)
            {
                var baseName = Path.GetFileNameWithoutExtension(cleaned);
                var keep = MaxLength - extension.Length;

                if (keep < 1)
                {
                    return null;
                }

                cleaned = baseName[..Math.Min(baseName.Length, keep)] + extension;
            }

            return cleaned;
        }
    }


}
