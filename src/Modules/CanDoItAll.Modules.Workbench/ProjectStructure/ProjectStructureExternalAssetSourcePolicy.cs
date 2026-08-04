using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureExternalAssetSourcePolicy
{
    public const string HttpClientName = "ProjectStructureExternalAssetSource";
    public const int MaximumRedirects = 5;
    private const int MaximumDisplayLength = 512;

    public static void ValidateUri(Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        if (!sourceUri.IsAbsoluteUri ||
            !(sourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              sourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(sourceUri.Host))
        {
            throw new ProjectStructureExternalAssetSourcePolicyException(
                "External asset sources must use an absolute http or https URL.");
        }

        if (!string.IsNullOrWhiteSpace(sourceUri.UserInfo))
        {
            throw new ProjectStructureExternalAssetSourcePolicyException(
                "External asset source URLs must not contain embedded credentials.");
        }

        var host = sourceUri.IdnHost;
        if (sourceUri.IsLoopback ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectStructureExternalAssetSourcePolicyException(
                "External asset sources must resolve only to public addresses.");
        }

        if (IPAddress.TryParse(host, out var address))
        {
            EnsurePublicAddress(address);
        }
    }

    public static void EnsurePublicAddresses(IReadOnlyCollection<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        if (addresses.Count == 0)
        {
            throw new ProjectStructureExternalAssetSourcePolicyException(
                "External asset source DNS resolution returned no addresses.");
        }

        foreach (var address in addresses)
        {
            EnsurePublicAddress(address);
        }
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] != 0 &&
                   bytes[0] != 10 &&
                   bytes[0] != 127 &&
                   !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                   !(bytes[0] == 169 && bytes[1] == 254) &&
                   !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                   !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) &&
                   !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) &&
                   !(bytes[0] == 192 && bytes[1] == 168) &&
                   !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
                   !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                   !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) &&
                   bytes[0] < 224;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) ||
            address.Equals(IPAddress.IPv6Loopback) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast)
        {
            return false;
        }

        var ipv6Bytes = address.GetAddressBytes();
        var isGlobalUnicast = (ipv6Bytes[0] & 0b1110_0000) == 0b0010_0000;
        var isDocumentationRange = ipv6Bytes[0] == 0x20 &&
                                   ipv6Bytes[1] == 0x01 &&
                                   ipv6Bytes[2] == 0x0d &&
                                   ipv6Bytes[3] == 0xb8;
        return isGlobalUnicast && !isDocumentationRange;
    }

    public static string FormatForDisplay(Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        var display = sourceUri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.UriEscaped);
        return display.Length <= MaximumDisplayLength
            ? display
            : $"{display[..(MaximumDisplayLength - 3)]}...";
    }

    private static void EnsurePublicAddress(IPAddress address)
    {
        if (!IsPublicAddress(address))
        {
            throw new ProjectStructureExternalAssetSourcePolicyException(
                "External asset sources must resolve only to public addresses.");
        }
    }
}

internal static class ProjectStructureExternalAssetSourceHttpClient
{
    public static SocketsHttpHandler CreatePrimaryHandler()
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "External project-structure asset downloads require the server runtime.");
        }

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = ConnectToVettedAddressAsync
        };
    }

    [UnsupportedOSPlatform("browser")]
    private static async ValueTask<Stream> ConnectToVettedAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "External project-structure asset downloads require the server runtime.");
        }

        var endpoint = context.DnsEndPoint;
        var addresses = IPAddress.TryParse(endpoint.Host, out var literalAddress)
            ? [literalAddress]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);

        ProjectStructureExternalAssetSourcePolicy.EnsurePublicAddresses(addresses);

        Exception? lastFailure = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                socket.Dispose();
                lastFailure = exception;
            }
        }

        throw new HttpRequestException(
            $"No vetted address for external asset source host '{endpoint.Host}' accepted the connection.",
            lastFailure);
    }
}

internal sealed class ProjectStructureExternalAssetSourcePolicyException(string message) : Exception(message);
