using System.Net;

namespace CanDoItAll.Web.Infrastructure;

internal sealed class LocalOperatorUiOptions {
    public const string SectionName = "WebHost:LocalOperatorUi";

    public string[] TrustedAddresses { get; set; } = [];

    internal bool IsValid() => TrustedAddresses is not null && TrustedAddresses.All(value =>
        IPAddress.TryParse(value, out var address) &&
        !Normalize(address).Equals(IPAddress.Any) &&
        !address.Equals(IPAddress.IPv6Any) &&
        !Normalize(address).Equals(IPAddress.Broadcast));

    internal static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
