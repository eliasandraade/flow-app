using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

// .NET 8 introduced System.Net.IPNetwork alongside the one the middleware has always used.
// The alias picks the middleware's type deliberately rather than leaving it ambiguous.
using ProxyNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Flow.API;

/// <summary>
/// Which proxies Flow is willing to believe.
///
/// Behind Traefik the TCP peer is the proxy, not the user. Without processing forwarded
/// headers the application sees the proxy's address for every request and the scheme as
/// plain http even though the client is on https. Both are load-bearing here: the
/// authentication rate limiter partitions by client address, and HTTPS redirection reads
/// the scheme.
///
/// The dangerous half is who gets believed. A forwarded header is just a header — anyone
/// can send one — so it may only be trusted when the request actually arrived from a proxy
/// we operate. That is why this is a list of addresses and networks rather than a switch.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Off by default. Running without a proxy and trusting forwarded headers would let any
    /// client claim any address, which is strictly worse than not reading them at all.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Exact proxy addresses, for a fixed deployment.</summary>
    public string[] TrustedProxies { get; set; } = [];

    /// <summary>
    /// Trusted ranges in CIDR notation, which is what container platforms need: the proxy
    /// gets an address from the bridge network and it changes between deployments.
    /// </summary>
    public string[] TrustedNetworks { get; set; } = [];

    /// <summary>
    /// How many entries to walk from the right of the header. One proxy, one entry. Raising
    /// it means trusting that many hops, so it stays bounded rather than disabled.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>Whether the proxy's Host header should be honoured as well.</summary>
    public bool TrustForwardedHost { get; set; }

    /// <summary>
    /// The entries an operator actually wrote.
    ///
    /// docker-compose.yml materialises TrustedNetworks__0 and TrustedProxies__0 whether or
    /// not the corresponding variable was set, so the one left alone arrives as an empty
    /// string instead of being absent. Behind Traefik that is the normal case: the guide
    /// says to configure the network and leave the proxy list alone.
    ///
    /// A blank entry is an operator who said nothing, so it is dropped before anything else
    /// looks at the list. Dropping it is not the same as being permissive — what survives
    /// is still an explicit allow-list, and a list that ends up empty is an unconfigured
    /// proxy, which <see cref="ForwardedHeadersSetup.Validate"/> refuses in production.
    /// </summary>
    public IReadOnlyList<string> EffectiveTrustedProxies => Meaningful(TrustedProxies);

    /// <inheritdoc cref="EffectiveTrustedProxies"/>
    public IReadOnlyList<string> EffectiveTrustedNetworks => Meaningful(TrustedNetworks);

    public bool HasTrustedSources =>
        EffectiveTrustedProxies.Count > 0 || EffectiveTrustedNetworks.Count > 0;

    private static IReadOnlyList<string> Meaningful(string[]? entries) =>
        entries is null
            ? []
            : entries.Where(entry => !string.IsNullOrWhiteSpace(entry))
                     .Select(entry => entry.Trim())
                     .ToArray();
}

public static class ForwardedHeadersSetup
{
    /// <summary>
    /// Translates the settings into the framework's options.
    ///
    /// The defaults it replaces trust only loopback, which is correct for a process behind a
    /// proxy on the same machine and useless in a container, where the proxy arrives from
    /// the bridge network. What it deliberately does not do is clear those defaults and
    /// trust everyone: that turns a header the client controls into the client's identity.
    /// </summary>
    public static void Apply(ForwardedHeadersOptions options, ForwardedHeadersSettings settings)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        if (settings.TrustForwardedHost)
            options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;

        options.ForwardLimit = settings.ForwardLimit;

        if (!settings.HasTrustedSources) return;

        // Replacing the loopback defaults, not clearing them into an empty list: what
        // follows is an explicit allow-list, and a request from anywhere else keeps being
        // read from its real connection.
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();

        // Blank entries were already dropped; everything left is something the operator
        // meant, so a value that does not parse is a mistake and stops startup.
        foreach (var proxy in settings.EffectiveTrustedProxies)
        {
            if (!IPAddress.TryParse(proxy, out var address))
                throw new InvalidOperationException(
                    $"ForwardedHeaders:TrustedProxies contains an entry that is not an IP address: '{proxy}'.");

            options.KnownProxies.Add(address);
        }

        foreach (var network in settings.EffectiveTrustedNetworks)
            options.KnownNetworks.Add(ParseNetwork(network));
    }

    /// <summary>
    /// Validated at startup rather than at the first request, because a proxy setting that
    /// is quietly ineffective is the worst outcome: everything appears to work while every
    /// client shares one rate-limit bucket and the audit trail records the proxy's address.
    /// </summary>
    public static void Validate(ForwardedHeadersSettings settings, bool isDevelopment)
    {
        if (!settings.Enabled || isDevelopment) return;

        if (!settings.HasTrustedSources)
        {
            throw new InvalidOperationException(
                "ForwardedHeaders:Enabled is true but no ForwardedHeaders:TrustedProxies or "
                + "ForwardedHeaders:TrustedNetworks were configured, or every entry was "
                + "empty. Only loopback would be trusted, so forwarded headers would be "
                + "silently ignored behind a proxy. Configure the proxy addresses or the "
                + "network it runs on.");
        }

        if (settings.ForwardLimit < 1)
        {
            throw new InvalidOperationException(
                "ForwardedHeaders:ForwardLimit must be at least 1.");
        }
    }

    private static ProxyNetwork ParseNetwork(string cidr)
    {
        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var length))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:TrustedNetworks contains an entry that is not CIDR notation: '{cidr}'.");
        }

        var maxLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;

        if (length < 0 || length > maxLength)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:TrustedNetworks prefix length is out of range for '{cidr}'.");
        }

        return new ProxyNetwork(prefix, length);
    }
}
