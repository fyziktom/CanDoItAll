using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentEditorCommandsTests {
    [Fact]
    public async Task Root_preparation_rejection_never_reaches_the_write_port() {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceProbe>();
        var probe = (WorkspaceProbe)(object)workspace;
        var commands = new AgentEditorCommands(workspace, new RejectingRootRegistryFactory());
        var draft = new AgentEditorModel { Name = "Preserved after rejection" };
        var result = Assert.IsType<AgentEditorSaveOutcome.Rejected>(
            await commands.SaveAsync(AgentEditorDraftPolicy.Capture(draft, [], []).Request));
        Assert.False(result.IsConflict);
        Assert.Equal("Root mapping unavailable.", result.Message);
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
            => throw new InvalidOperationException("Root mapping unavailable.");
    }
}
