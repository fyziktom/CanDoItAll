using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessCancellationRecoveryRegistrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Worker_respects_host_lane(bool mcp) {
        var services = Register(new() {
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = mcp ? LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind : null
        });
        Assert.Equal(!mcp, services.Any(item => item.ServiceType == typeof(IHostedService) && item.ImplementationType == typeof(ProcessCancellationRecoveryWorker)));
    }

    [Fact]
    public void Options_bind_nested_limits_and_remain_disabled_by_default() {
        using var defaults = Register([]).BuildServiceProvider();
        Assert.False(defaults.GetRequiredService<IOptions<ProcessCancellationRecoveryOptions>>().Value.Enabled);
        using var configured = Register(new() {
            [Key(nameof(ProcessCancellationRecoveryOptions.Enabled))] = "true",
            [Key($"{nameof(ProcessCancellationRecoveryOptions.Limits)}:MaximumOperations")] = "5"
        }).BuildServiceProvider();
        var options = configured.GetRequiredService<IOptions<ProcessCancellationRecoveryOptions>>().Value;
        Assert.True(options.Enabled);
        Assert.Equal(5, options.Limits.MaximumOperations);
        Assert.Equal(TimeSpan.FromSeconds(2), options.Limits.MaximumDuration);
        Assert.Equal(8, ProcessCancellationRecoverySliceLimits.Default.MaximumOperations);
    }

    [Theory]
    [InlineData("IdleDelay", "00:00:00")]
    [InlineData("LegacySeedPageSize", "65")]
    [InlineData("Limits:YieldDelay", "00:00:00")]
    [InlineData("Limits:LeaseDuration", "00:00:01")]
    public void Invalid_scheduling_limits_fail_explicitly(string member, string value) {
        using var provider = Register(new() { [Key(member)] = value }).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ProcessCancellationRecoveryOptions>>().Value);
    }

    private static string Key(string member) => $"{ProcessCancellationRecoveryOptions.ConfigurationSectionName}:{member}";

    private static IServiceCollection Register(Dictionary<string, string?> values) {
        var services = new ServiceCollection();
        services.AddProcessesModule(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services;
    }
}
