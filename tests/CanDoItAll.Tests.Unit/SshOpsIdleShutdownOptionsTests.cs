using CanDoItAll.Mcp.SshOps.Configuration;

namespace CanDoItAll.Tests.Unit;

public sealed class SshOpsIdleShutdownOptionsTests
{
    [Fact]
    public void SshOps_Default_Timeout_Is_Longer_For_Remote_Operations()
    {
        var options = new McpServerOptions();

        Assert.True(options.Server.IdleShutdown.Enabled);
        Assert.Equal(1_800, options.Server.IdleShutdown.InactivityTimeoutSeconds);
        Assert.Equal(30, options.Server.IdleShutdown.CheckIntervalSeconds);
    }
}
