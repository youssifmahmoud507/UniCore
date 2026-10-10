namespace UniCore.Infrastructure.Modules.Email
{
    public sealed class SmtpOptions
    {
        public const string SectionName = "Smtp";

        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 1025;
        public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.None;

        // Secrets: user-secrets or environment variables in real environments.
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string FromName { get; set; } = "UniCore";
        public string FromAddress { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
    }



}
