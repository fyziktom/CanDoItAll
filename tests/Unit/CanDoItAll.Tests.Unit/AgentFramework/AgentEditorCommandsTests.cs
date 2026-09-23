using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentEditorCommandsTests {
    [Fact]
    public async Task Reference_read_adapter_replaces_private_errors_before_returning_its_presentation_result() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ReferenceProbe>();
        var providers = DispatchProxy.Create<CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService, ReferenceProbe>();
        var access = DispatchProxy.Create<IAgentEditorAccessQuery, ReferenceProbe>();
        var reads = new AgentEditorReads(workspace, providers, access);
        var result = await reads.LoadAsync(AgentEditorTarget.Create);
        Assert.NotNull(result.Providers.Error);
        Assert.NotNull(result.Secrets.Error);
        Assert.DoesNotContain("REFERENCE_PRIVATE_SENTINEL", result.Providers.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("REFERENCE_PRIVATE_SENTINEL", result.Secrets.Error, StringComparison.Ordinal);
        Assert.True(result.Draft.Id is null);
    }

    public class ReferenceProbe : DispatchProxy {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch {
            nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) => Task.FromResult<IReadOnlyList<AgentDefinition>>([]),
            nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) => Task.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]),
            nameof(CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService.ListProvidersAsync)
                => Task.FromException<IReadOnlyList<ProviderProfile>>(new IOException("REFERENCE_PRIVATE_SENTINEL /srv/private/provider")),
            nameof(IAgentEditorAccessQuery.ReadSecretsAsync)
                => Task.FromException<IReadOnlyList<AgentEditorSecret>>(new IOException("REFERENCE_PRIVATE_SENTINEL api_key=test-only-private-reference")),
            _ => throw new InvalidOperationException("Unexpected editor reference call.")
        };
    }
    [Fact]
    public async Task Unconfirmed_write_does_not_expose_private_exception_detail() {
        const string poison = "COMMAND_PRIVATE_SENTINEL api_key=test-only-private-command /srv/private/save at Internal.Save()";
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProbe>();
        var probe = (WorkspaceProbe)(object)workspace;
        probe.Failure = new IOException(poison);
        var commands = new AgentEditorCommands(workspace, new ExternalTargetPathRegistryFactory());
        var outcome = Assert.IsType<AgentEditorSaveOutcome.Unconfirmed>(await commands.SaveAsync(new()));
        Assert.DoesNotContain("COMMAND_PRIVATE_SENTINEL", outcome.Message, StringComparison.Ordinal);
        Assert.Equal(1, probe.Calls);
    }
    [Fact]
    public async Task Root_preparation_rejection_never_reaches_the_write_port() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProbe>();
        var probe = (WorkspaceProbe)(object)workspace;
        var commands = new AgentEditorCommands(workspace, new RejectingRootRegistryFactory());
        var draft = new AgentEditorModel { Name = "Preserved after rejection" };
        var result = Assert.IsType<AgentEditorSaveOutcome.Rejected>(
            await commands.SaveAsync(AgentEditorDraftPolicy.Capture(draft, [], []).Request));
        Assert.False(result.IsConflict);
        Assert.Equal("External workspace roots could not be prepared. Review the selected paths and bindings.", result.Message);
        Assert.DoesNotContain("ROOT_PRIVATE_SENTINEL", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, probe.Calls);
        Assert.Equal("Preserved after rejection", draft.Name);
    }

    [Theory]
    [InlineData(WriteFailure.Validation)]
    [InlineData(WriteFailure.Conflict)]
    [InlineData(WriteFailure.UnknownIo)]
    [InlineData(WriteFailure.UnknownInvalidOperation)]
    [InlineData(WriteFailure.UnownedCancellation)]
    public async Task Only_typed_known_rejections_allow_replay(WriteFailure failure) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProbe>();
        var probe = (WorkspaceProbe)(object)workspace;
        probe.Failure = failure switch {
            WriteFailure.Validation => new AgentEditorValidationException("Known pre-write rejection."),
            WriteFailure.Conflict => new AgentCatalogConcurrencyException(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null),
            WriteFailure.UnknownIo => new IOException("Unknown write outcome."),
            WriteFailure.UnknownInvalidOperation => new InvalidOperationException("Unknown persistence operation."),
            WriteFailure.UnownedCancellation => new OperationCanceledException("Not cancelled by the editor."),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        var commands = new AgentEditorCommands(workspace, new ExternalTargetPathRegistryFactory());
        var outcome = await commands.SaveAsync(new());
        if (failure is WriteFailure.Validation or WriteFailure.Conflict) {
            Assert.Equal(failure == WriteFailure.Conflict, Assert.IsType<AgentEditorSaveOutcome.Rejected>(outcome).IsConflict);
        } else {
            Assert.IsType<AgentEditorSaveOutcome.Unconfirmed>(outcome);
        }
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task Owner_cancellation_during_save_propagates_without_becoming_unconfirmed() {
        using var cancellation = new CancellationTokenSource();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProbe>();
        var probe = (WorkspaceProbe)(object)workspace;
        probe.Cancel = cancellation.Cancel;
        probe.Failure = new OperationCanceledException(cancellation.Token);
        var commands = new AgentEditorCommands(workspace, new ExternalTargetPathRegistryFactory());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => commands.SaveAsync(new(), cancellation.Token));
        Assert.Equal(1, probe.Calls);
    }

    public enum WriteFailure { Validation, Conflict, UnknownIo, UnknownInvalidOperation, UnownedCancellation }

    public class WorkspaceProbe : DispatchProxy {
        public int Calls { get; private set; }
        public Exception Failure { get; set; } = new InvalidOperationException("Unexpected write.");
        public Action? Cancel { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            Assert.Equal(nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync), targetMethod?.Name);
            Calls++;
            Cancel?.Invoke();
            return Task.FromException<Guid>(Failure);
        }
    }

    private sealed class RejectingRootRegistryFactory : IExternalTargetPathRegistryFactory {
        public IExternalTargetPathRegistry Create(IEnumerable<ExternalTargetRootBinding> bindings)
            => throw new InvalidOperationException("ROOT_PRIVATE_SENTINEL /srv/private/root api_key=test-only-private-root");
    }
}
