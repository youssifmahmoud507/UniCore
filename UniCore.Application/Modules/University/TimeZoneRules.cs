namespace UniCore.Application.Modules.University
{
    // The domain only checks the format of a time zone id. This checks that the server really knows it.
    public static class TimeZoneRules
    {
        public static bool Exists(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return false;
            }

            // TryFind... returns false instead of throwing when the id is unknown.
            return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out _);
        }
    }
}
