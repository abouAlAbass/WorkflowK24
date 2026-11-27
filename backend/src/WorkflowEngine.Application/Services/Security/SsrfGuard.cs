using System.Net;
using System.Net.Sockets;

namespace WorkflowEngine.Application.Services.Security;

public record SsrfValidationResult(bool IsAllowed, string? BlockReason = null)
{
    public static SsrfValidationResult Allowed() => new(true);
    public static SsrfValidationResult Blocked(string reason) => new(false, reason);
}

/// <summary>
/// Abstraction over DNS resolution so SSRF post-DNS validation can be tested deterministically.
/// </summary>
public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default);
}

public class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken ct = default)
    {
        return Dns.GetHostAddressesAsync(host, ct);
    }
}

/// <summary>
/// Strict SSRF guard verifying scheme, AllowedHosts whitelist, and post-DNS IP safety.
/// Blocks loopback, private RFC1918 ranges, link-local / cloud metadata (169.254.0.0/16),
/// IPv6 unique local/link-local/loopback, unspecified, CGNAT, and multicast addresses.
/// </summary>
public interface ISsrfGuard
{
    Task<SsrfValidationResult> ValidateAsync(Uri targetUri, IReadOnlyList<string> allowedHosts, CancellationToken ct = default);
}

public class SsrfGuard : ISsrfGuard
{
    private readonly IDnsResolver _dnsResolver;

    public SsrfGuard(IDnsResolver dnsResolver)
    {
        _dnsResolver = dnsResolver ?? throw new ArgumentNullException(nameof(dnsResolver));
    }

    public async Task<SsrfValidationResult> ValidateAsync(
        Uri targetUri,
        IReadOnlyList<string> allowedHosts,
        CancellationToken ct = default)
    {
        if (targetUri == null || !targetUri.IsAbsoluteUri)
        {
            return SsrfValidationResult.Blocked("Target URI must be absolute.");
        }

        if (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps)
        {
            return SsrfValidationResult.Blocked($"Scheme '{targetUri.Scheme}' is not allowed. Only HTTP and HTTPS are permitted.");
        }

        string host = targetUri.DnsSafeHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            return SsrfValidationResult.Blocked("Target URI has an empty host.");
        }

        // 1. Strict AllowedHosts whitelist check
        if (allowedHosts == null || allowedHosts.Count == 0 || !IsHostWhitelisted(host, allowedHosts))
        {
            return SsrfValidationResult.Blocked($"Host '{host}' is not in the connection's allowed_hosts whitelist.");
        }

        // 2. Direct IP literal check
        if (IPAddress.TryParse(host, out var literalIp))
        {
            if (IsForbiddenIpAddress(literalIp, out string reason))
            {
                return SsrfValidationResult.Blocked($"Target IP '{literalIp}' is blocked by SSRF policy ({reason}).");
            }

            return SsrfValidationResult.Allowed();
        }

        // 3. Post-DNS resolution check: resolve and validate EVERY returned IP
        IPAddress[] resolvedAddresses;
        try
        {
            resolvedAddresses = await _dnsResolver.GetHostAddressesAsync(host, ct);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return SsrfValidationResult.Blocked($"DNS resolution failed for host '{host}': {ex.Message}");
        }

        if (resolvedAddresses == null || resolvedAddresses.Length == 0)
        {
            return SsrfValidationResult.Blocked($"DNS resolution returned no IP addresses for host '{host}'.");
        }

        foreach (var address in resolvedAddresses)
        {
            if (IsForbiddenIpAddress(address, out string reason))
            {
                return SsrfValidationResult.Blocked(
                    $"Host '{host}' resolved to forbidden IP '{address}' ({reason}).");
            }
        }

        return SsrfValidationResult.Allowed();
    }

    public static bool IsHostWhitelisted(string host, IReadOnlyList<string> allowedHosts)
    {
        string normalizedHost = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var rawPattern in allowedHosts)
        {
            if (string.IsNullOrWhiteSpace(rawPattern)) continue;
            string pattern = rawPattern.Trim().TrimEnd('.').ToLowerInvariant();

            if (pattern == normalizedHost)
            {
                return true;
            }

            if (pattern.StartsWith("*.", StringComparison.Ordinal) && pattern.Length > 2)
            {
                string suffix = pattern[1..]; // ".example.com"
                if (normalizedHost.EndsWith(suffix, StringComparison.Ordinal) &&
                    normalizedHost.Length > suffix.Length)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsForbiddenIpAddress(IPAddress address, out string reason)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            reason = "loopback";
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = address.GetAddressBytes();
            // 0.0.0.0/8 (unspecified / "this" network)
            if (b[0] == 0)
            {
                reason = "unspecified 0.0.0.0/8";
                return true;
            }
            // 10.0.0.0/8 (RFC1918 private)
            if (b[0] == 10)
            {
                reason = "private 10.0.0.0/8";
                return true;
            }
            // 100.64.0.0/10 (CGNAT / shared address space)
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            {
                reason = "shared CGNAT 100.64.0.0/10";
                return true;
            }
            // 127.0.0.0/8 (loopback)
            if (b[0] == 127)
            {
                reason = "loopback 127.0.0.0/8";
                return true;
            }
            // 169.254.0.0/16 (link-local & cloud metadata 169.254.169.254)
            if (b[0] == 169 && b[1] == 254)
            {
                reason = "link-local / cloud metadata 169.254.0.0/16";
                return true;
            }
            // 172.16.0.0/12 (RFC1918 private)
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            {
                reason = "private 172.16.0.0/12";
                return true;
            }
            // 192.168.0.0/16 (RFC1918 private)
            if (b[0] == 192 && b[1] == 168)
            {
                reason = "private 192.168.0.0/16";
                return true;
            }
            // 224.0.0.0/4 (multicast) and 240.0.0.0/4 (reserved/broadcast)
            if (b[0] >= 224)
            {
                reason = "multicast or reserved";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any))
            {
                reason = "unspecified IPv6";
                return true;
            }

            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            {
                reason = "link-local/site-local/multicast IPv6";
                return true;
            }

            byte[] b = address.GetAddressBytes();
            // fc00::/7 (Unique Local Address)
            if ((b[0] & 0xFE) == 0xFC)
            {
                reason = "unique-local IPv6 fc00::/7";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        reason = "unsupported address family";
        return true;
    }
}
