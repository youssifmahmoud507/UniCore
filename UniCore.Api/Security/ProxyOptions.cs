using System.Net;

namespace UniCore.Api.Security
{
    // appsettings: "Proxy": { "TrustedNetworks": [ "10.0.0.0/8", "172.18.0.5" ] }
    // Behind a reverse proxy the real client address comes from X-Forwarded-For, but ONLY when the proxy is trusted.
    // Empty (the default): the header is ignored, so nobody can fake an address to dodge the rate limits.
    public sealed class ProxyOptions
    {
        public const string SectionName = "Proxy";

        public string[] TrustedNetworks { get; set; } = [];

        public static bool IsValid(ProxyOptions options) =>
            (options.TrustedNetworks ?? []).All(entry => TryParse(entry, out _, out _));

        // A CIDR range ("10.0.0.0/8") or one address ("172.18.0.5").
        public static bool TryParse(string? entry, out System.Net.IPNetwork network, out IPAddress? address)
        {
            network = default;
            address = null;

            if (string.IsNullOrWhiteSpace(entry))
            {
                return false;
            }

            if (entry.Contains('/'))
            {
                return System.Net.IPNetwork.TryParse(entry, out network);
            }

            return IPAddress.TryParse(entry, out address);
        }
    }



}
