using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class CapabilityWorkspaceResolutionTests {
    [Fact]
    public async Task Profile_resolution_failure_is_typed_unavailable_before_workspace_dispatch() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var factory = new UnavailableWorkspaceFactory();
        using var workspace = ActivatorUtilities.CreateInstance<CurrentProfileAgentFrameworkWorkspaceService>(scope.ServiceProvider, factory);
        var failure = await Assert.ThrowsAsync<CapabilityVerificationException>(() => workspace.VerifyCapabilityAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(CapabilityVerificationDisposition.InfrastructureUnavailable, failure.Outcome.Disposition);
        Assert.Null(failure.Outcome.Receipt);
        Assert.Equal(0, factory.WorkspaceDispatches);
        Assert.DoesNotContain("private profile fixture", failure.Message);
    }

    private sealed class UnavailableWorkspaceFactory : ICanDoItAllAgentWorkspaceFactory {
        public int WorkspaceDispatches { get; private set; }
        public WorkspaceScopeDescriptor GetOrganizationScope() => throw new IOException("private profile fixture unavailable");
        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) {
            WorkspaceDispatches++;
            throw new InvalidOperationException("Must not dispatch after profile resolution fails.");
        }
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => throw new NotSupportedException();
        public string GetWorkspaceRoot() => throw new NotSupportedException();
    }
}
