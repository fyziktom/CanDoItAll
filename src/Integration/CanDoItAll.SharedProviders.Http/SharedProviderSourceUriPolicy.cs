using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public sealed class SharedProviderSourceUriPolicy : ISharedProviderSourceUriPolicy
{
    public const int MaximumUriCharacters = 2_048;

    public Uri Normalize(
        Uri sourceBaseUri,
        SharedProviderSourceNetworkPolicy networkPolicy)
    {
        ArgumentNullException.ThrowIfNull(sourceBaseUri);
        if (!Enum.IsDefined(networkPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(networkPolicy));
        }

        var original = sourceBaseUri.OriginalString;
        if (original.Length is 0 or > MaximumUriCharacters ||
            original.Contains('\\') ||
            original.Contains('?') ||
            original.Contains('#') ||
            !sourceBaseUri.IsAbsoluteUri ||
            !IsHttpScheme(sourceBaseUri.Scheme) ||
            string.IsNullOrWhiteSpace(sourceBaseUri.Host) ||
            sourceBaseUri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6) ||
            !string.IsNullOrEmpty(sourceBaseUri.UserInfo) ||
            HasAuthorityUserInfo(original))
        {
            throw new ArgumentException(
                "A source base URI must be an unambiguous absolute HTTP or HTTPS URI without userinfo, query, or fragment.",
                nameof(sourceBaseUri));
        }

        string host;
        try
        {
            host = sourceBaseUri.IdnHost.ToLowerInvariant();
        }
        catch (UriFormatException exception)
        {
            throw new ArgumentException("The source base URI host is invalid.", nameof(sourceBaseUri), exception);
        }

        var escapedHost = sourceBaseUri.GetComponents(UriComponents.Host, UriFormat.UriEscaped);
        var escapedPath = sourceBaseUri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        if (host.Length is 0 or > 253 ||
            host.EndsWith(".", StringComparison.Ordinal) ||
            escapedHost.Contains('%') ||
            ContainsEscapedPathSeparator(escapedPath))
        {
            throw new ArgumentException("The source base URI contains an ambiguous host or path.", nameof(sourceBaseUri));
        }

        var isLoopbackHost = IsLoopbackHost(host);
        if (sourceBaseUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !isLoopbackHost &&
            networkPolicy != SharedProviderSourceNetworkPolicy.AllowPrivateNetwork)
        {
            throw new ArgumentException(
                "Plain HTTP is allowed only for loopback or an explicitly approved private-network source.",
                nameof(sourceBaseUri));
        }

        if (IPAddress.TryParse(host, out var literalAddress))
        {
            EnsureLiteralDestinationAllowed(
                literalAddress,
                networkPolicy,
                sourceBaseUri.Scheme,
                nameof(sourceBaseUri));
        }
        else if (isLoopbackHost &&
            sourceBaseUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            networkPolicy == SharedProviderSourceNetworkPolicy.PublicOnly)
        {
            throw new ArgumentException(
                "A loopback HTTPS source requires explicit private-network approval.",
                nameof(sourceBaseUri));
        }

        var scheme = sourceBaseUri.Scheme.ToLowerInvariant();
        var authorityHost = literalAddress?.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{host}]"
            : host;
        var port = sourceBaseUri.IsDefaultPort ? string.Empty : $":{sourceBaseUri.Port}";
        var path = escapedPath.Trim('/');
        var canonical = path.Length == 0
            ? $"{scheme}://{authorityHost}{port}/"
            : $"{scheme}://{authorityHost}{port}/{path}/";
        return new Uri(canonical, UriKind.Absolute);
    }

    internal static bool IsLoopbackHost(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    private static bool IsHttpScheme(string scheme)
        => scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool HasAuthorityUserInfo(string original)
    {
        var schemeSeparator = original.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator < 0)
        {
            return false;
        }

        var authorityStart = schemeSeparator + 3;
        var authorityEnd = original.IndexOf('/', authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = original.Length;
        }

        return original.AsSpan(authorityStart, authorityEnd - authorityStart).Contains('@');
    }

    private static bool ContainsEscapedPathSeparator(string path)
        => path.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("%5c", StringComparison.OrdinalIgnoreCase);

    private static void EnsureLiteralDestinationAllowed(
        IPAddress address,
        SharedProviderSourceNetworkPolicy networkPolicy,
        string scheme,
        string parameterName)
    {
        var isHttp = scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        var access = isHttp
            ? SharedProviderDestinationAccess.ApprovedPrivateOnly
            : networkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork
                ? SharedProviderDestinationAccess.TrustedNetwork
                : SharedProviderDestinationAccess.PublicOnly;
        if (!SharedProviderSourceAddressPolicy.IsAllowed(address, access))
        {
            throw new ArgumentException(
                "The source base URI resolves to a destination outside the selected network policy.",
                parameterName);
        }
    }
}

internal enum SharedProviderDestinationAccess
{
    PublicOnly,
    TrustedNetwork,
    ApprovedPrivateOnly
}

internal static class SharedProviderSourceAddressPolicy
{
    public static bool IsAllowed(
        IPAddress address,
        SharedProviderDestinationAccess access)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access));
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return access switch
        {
            SharedProviderDestinationAccess.PublicOnly => IsPublic(address),
            SharedProviderDestinationAccess.TrustedNetwork =>
                IsPublic(address) || IsApprovedPrivate(address),
            SharedProviderDestinationAccess.ApprovedPrivateOnly => IsApprovedPrivate(address),
            _ => false
        };
    }

    private static bool IsPublic(IPAddress address)
    {
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
                !(bytes[0] == 192 && bytes[1] == 88 && bytes[2] == 99) &&
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

        var bytesV6 = address.GetAddressBytes();
        var isGlobalUnicast = (bytesV6[0] & 0b1110_0000) == 0b0010_0000;
        var isIetfProtocolAssignment = bytesV6[0] == 0x20 &&
            bytesV6[1] == 0x01 &&
            (bytesV6[2] & 0xfe) == 0;
        var isDocumentationRange = bytesV6[0] == 0x20 &&
            bytesV6[1] == 0x01 &&
            bytesV6[2] == 0x0d &&
            bytesV6[3] == 0xb8;
        var isSixToFour = bytesV6[0] == 0x20 && bytesV6[1] == 0x02;
        var isExtendedDocumentationRange = bytesV6[0] == 0x3f &&
            bytesV6[1] == 0xff &&
            (bytesV6[2] & 0xf0) == 0;
        return isGlobalUnicast &&
            !isIetfProtocolAssignment &&
            !isDocumentationRange &&
            !isSixToFour &&
            !isExtendedDocumentationRange;
    }

    private static bool IsApprovedPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast)
        {
            return false;
        }

        var bytesV6 = address.GetAddressBytes();
        return (bytesV6[0] & 0b1111_1110) == 0b1111_1100;
    }
}

internal interface ISharedProviderHostAddressResolver
{
    ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken);
}

internal sealed class SystemSharedProviderHostAddressResolver : ISharedProviderHostAddressResolver
{
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken)
        => IPAddress.TryParse(host, out var literalAddress)
            ? [literalAddress]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
}

internal interface ISharedProviderSocketConnector
{
    ValueTask<Stream> ConnectAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken);
}

internal sealed class SystemSharedProviderSocketConnector : ISharedProviderSocketConnector
{
    [UnsupportedOSPlatform("browser")]
    public async ValueTask<Stream> ConnectAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "Shared-provider source connections require the server runtime.");
        }

        Exception? lastFailure = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
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
            "No policy-approved shared-provider source destination accepted the connection.",
            lastFailure);
    }
}

internal sealed class SharedProviderSourceConnectionPolicy(
    SharedProviderDestinationAccess access,
    ISharedProviderHostAddressResolver resolver,
    ISharedProviderSocketConnector connector)
{
    [UnsupportedOSPlatform("browser")]
    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "Shared-provider source connections require the server runtime.");
        }

        ArgumentNullException.ThrowIfNull(context);
        return await ConnectAsync(context.DnsEndPoint, cancellationToken);
    }

    internal async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var addresses = await resolver.ResolveAsync(endpoint.Host, cancellationToken);
        if (addresses.Count == 0)
        {
            throw new HttpRequestException(
                "Shared-provider source DNS resolution returned no destinations.");
        }

        var effectiveAccess = SharedProviderSourceUriPolicy.IsLoopbackHost(endpoint.Host)
            ? SharedProviderDestinationAccess.TrustedNetwork
            : access;
        var loopbackOnly = SharedProviderSourceUriPolicy.IsLoopbackHost(endpoint.Host);
        if (addresses.Any(address =>
                !SharedProviderSourceAddressPolicy.IsAllowed(address, effectiveAccess) ||
                loopbackOnly && !IPAddress.IsLoopback(address)))
        {
            throw new HttpRequestException(
                "Shared-provider source DNS resolution returned a destination outside the selected network policy.");
        }

        return await connector.ConnectAsync(addresses, endpoint.Port, cancellationToken);
    }
}

internal static class SharedProviderSourceHttpHandlerFactory
{
    public static SocketsHttpHandler Create(
        SharedProviderDestinationAccess access,
        ISharedProviderHostAddressResolver resolver,
        ISharedProviderSocketConnector connector)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                "Shared-provider source connections require the server runtime.");
        }

        var connectionPolicy = new SharedProviderSourceConnectionPolicy(access, resolver, connector);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = connectionPolicy.ConnectAsync
        };
    }
}
