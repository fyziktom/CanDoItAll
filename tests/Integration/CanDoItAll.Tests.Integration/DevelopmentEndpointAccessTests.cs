using System.Net;
using CanDoItAll.Web;

namespace CanDoItAll.Tests.Integration;

public sealed class DevelopmentEndpointAccessTests
{
    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1", true)]
    [InlineData("::1", "::1", true)]
    [InlineData("203.0.113.10", "127.0.0.1", false)]
    [InlineData("127.0.0.1", "203.0.113.10", false)]
    [InlineData("203.0.113.10", "203.0.113.11", false)]
    public void Anonymous_access_requires_original_and_effective_loopback_addresses(
        string originalAddress,
        string effectiveAddress,
        bool expected)
    {
        var allowed = DevelopmentEndpointAccess.IsAnonymousLocalAccessAllowed(
            IPAddress.Parse(originalAddress),
            IPAddress.Parse(effectiveAddress));

        Assert.Equal(expected, allowed);
    }
}
