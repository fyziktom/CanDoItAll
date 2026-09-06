using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class CapabilityVerificationApiIntegrationTests {
    [Theory]
    [InlineData(CapabilityVerificationDisposition.Superseded)]
    [InlineData(CapabilityVerificationDisposition.Unconfirmed)]
    public async Task Verification_failure_preserves_target_identity_and_blocks_automatic_diagnostic_replay(CapabilityVerificationDisposition disposition) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CapabilityApiWorkspace>();
        ((CapabilityApiWorkspace)(object)workspace).Outcome = new(disposition);
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, configureServices: services => {
            services.Replace(ServiceDescriptor.Singleton(workspace));
        });
        var agentId = Guid.NewGuid();
        var capabilityId = Guid.NewGuid();
        using var response = await host.Client.PostAsJsonAsync($"/api/agents/{agentId:D}/capabilities/{capabilityId:D}/verify", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(agentId, json.RootElement.GetProperty("agentId").GetGuid());
        Assert.Equal(capabilityId, json.RootElement.GetProperty("capabilityId").GetGuid());
        Assert.False(json.RootElement.GetProperty("automaticReplaySafe").GetBoolean());
        Assert.DoesNotContain(nameof(CapabilityVerificationException), body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verification_success_retains_existing_ack_contract() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, CapabilityApiWorkspace>();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, configureServices: services => {
            services.Replace(ServiceDescriptor.Singleton(workspace));
        });
        using var response = await host.Client.PostAsJsonAsync($"/api/agents/{Guid.NewGuid():D}/capabilities/{Guid.NewGuid():D}/verify", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("true", await response.Content.ReadAsStringAsync());
    }
}

public class CapabilityApiWorkspace : DispatchProxy {
    public CapabilityVerificationOutcome? Outcome { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) {
        if (method!.Name == nameof(IAgentFrameworkWorkspaceService.VerifyCapabilityAsync)) {
            return Outcome is null ? Task.CompletedTask : Task.FromException(new CapabilityVerificationException(Outcome));
        }
        throw new InvalidOperationException("Unexpected API fixture operation.");
    }
}
