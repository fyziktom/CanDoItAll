using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration.Runtime;

[Flags]
internal enum AgentEffectActivityFault { None = 0, Report = 1, Suspend = 2, IsTerminal = 4, FailOrCancel = 8, Dispose = 16 }

internal sealed class AgentEffectActivityFaults(AgentEffectActivityFault selected) {
    internal bool Armed { get; set; }
    internal int Faults { get; private set; }
    internal int DisposeAttempts { get; private set; }
    internal bool ActualDisposed { get; private set; }
    internal List<Exception> Failures { get; } = [];

    internal IAgentExecutionActivityCoordinator Decorate(IAgentExecutionActivityCoordinator coordinator) => new Coordinator(coordinator, this);

    private void Fail(AgentEffectActivityFault point) {
        if (Armed && selected.HasFlag(point)) {
            Faults++;
            var failure = new TimeoutException($"Private injected activity {point} fault.");
            Failures.Add(failure);
            throw failure;
        }
    }

    private sealed class Coordinator(IAgentExecutionActivityCoordinator actual, AgentEffectActivityFaults faults) : IAgentExecutionActivityCoordinator {
        public AgentExecutionActivityAdmission AdmitOperation(AgentExecutionActivityStreamId streamId, Guid? agentId,
            Guid? chatSessionId, string acceptedMessage) {
            var admitted = actual.AdmitOperation(streamId, agentId, chatSessionId, acceptedMessage);
            return admitted is AgentExecutionActivityAdmitted accepted
                ? new AgentExecutionActivityAdmitted(accepted.StreamId, new Operation(accepted.Operation, faults)) : admitted;
        }
    }

    private sealed class Operation(IAgentExecutionActivityOperationLease actual, AgentEffectActivityFaults faults) : IAgentExecutionActivityOperationLease {
        public AgentExecutionActivityStreamId StreamId => actual.StreamId;
        public Guid? AgentId => actual.AgentId;
        public Guid? ChatSessionId => actual.ChatSessionId;
        public Guid? ExecutionRunId => actual.ExecutionRunId;
        public bool IsTerminal {
            get {
                faults.Fail(AgentEffectActivityFault.IsTerminal);
                return actual.IsTerminal;
            }
        }
        public void BindAgent(Guid agentId) => actual.BindAgent(agentId);
        public void BindContext(AgentChatContextSource source, long version) => actual.BindContext(source, version);
        public void BindChatSession(Guid sessionId) => actual.BindChatSession(sessionId);
        public void BindExecutionRun(Guid runId, Guid? sessionId = null) => actual.BindExecutionRun(runId, sessionId);
        public void Report(AgentExecutionActivityPhase phase, string message) {
            if (phase == AgentExecutionActivityPhase.AwaitingContinuation) {
                faults.Fail(AgentEffectActivityFault.Report);
            }
            actual.Report(phase, message);
        }
        public void Complete(string message) => actual.Complete(message);
        public void Fail(string message, string? errorCode = null) {
            faults.Fail(AgentEffectActivityFault.FailOrCancel);
            actual.Fail(message, errorCode);
        }
        public void Cancel(string message) {
            faults.Fail(AgentEffectActivityFault.FailOrCancel);
            actual.Cancel(message);
        }
        public void Suspend(string message) {
            faults.Fail(AgentEffectActivityFault.Suspend);
            actual.Suspend(message);
        }
        public void Dispose() {
            faults.DisposeAttempts++;
            actual.Dispose();
            faults.ActualDisposed = true;
            faults.Fail(AgentEffectActivityFault.Dispose);
        }
    }
}

internal sealed class AgentEffectCurrentProfileOwner : IDisposable {
    private readonly ServiceProvider services;
    private readonly IDisposable owner;

    internal AgentEffectCurrentProfileOwner(AgentEffectRecoveryFixture fixture, DatabaseProfileRecord profile,
        IAgentExecutionActivityCoordinator coordinator, Exception? synchronousFailure = null, Action? beforeSynchronousFailure = null) {
        services = new ServiceCollection().AddLogging().BuildServiceProvider();
        try {
            var type = typeof(AgentFrameworkModuleServiceCollectionExtensions).Assembly.GetType(
                "CanDoItAll.Modules.AgentFramework.CurrentProfileAgentFrameworkWorkspaceService", throwOnError: true)!;
            var scope = WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N"));
            IAgentFrameworkWorkspaceService workspace = synchronousFailure is null ? fixture.Service :
                Proxy<IAgentEffectRecoverySynchronousWorkspace>((method, arguments) => {
                    if (method.Name != nameof(IAgentFrameworkWorkspaceActivityExecutionService.RecoverExecutionRunWithinOperationAsync)) {
                        throw new InvalidOperationException($"Unexpected synchronous fixture operation: {method.Name}");
                    }
                    var operation = (IAgentExecutionActivityOperationLease)arguments![0]!;
                    operation.BindExecutionRun(fixture.Original.Id, fixture.Original.ChatSessionId);
                    beforeSynchronousFailure?.Invoke();
                    throw synchronousFailure;
                });
            var factory = Proxy<ICanDoItAllAgentWorkspaceFactory>((method, _) => method.Name switch {
                nameof(ICanDoItAllAgentWorkspaceFactory.GetOrganizationScope) => scope,
                nameof(ICanDoItAllAgentWorkspaceFactory.GetWorkspaceService) => workspace,
                _ => throw new InvalidOperationException($"Unexpected workspace factory operation: {method.Name}")
            });
            var accessor = Proxy<IDatabaseProfileRuntimeAccessor>((method, _) => method.Name == nameof(IDatabaseProfileRuntimeAccessor.ResolveCurrentProfile)
                ? new ResolvedDatabaseProfile(profile, DatabaseProfileResolutionSource.ExplicitOverride, "owned-recovery-fixture")
                : throw new InvalidOperationException($"Unexpected profile operation: {method.Name}"));
            owner = (IDisposable)ActivatorUtilities.CreateInstance(services, type, factory,
                Proxy<IAiTechnicalAgentBridge>((method, _) => throw new InvalidOperationException($"Unexpected technical agent operation: {method.Name}")),
                Proxy<IAgentReferenceDataCacheInvalidator>((method, _) => throw new InvalidOperationException($"Unexpected reference-data operation: {method.Name}")),
                accessor, new DatabaseSwitchNotificationService(), coordinator,
                new FixedAgentExecutionProfileGenerationSource(fixture.Identity.DatabaseProfileGeneration),
                services.GetRequiredService(typeof(ILogger<>).MakeGenericType(type)));
            Service = (IAgentFrameworkWorkspaceService)owner;
        } catch {
            services.Dispose();
            throw;
        }
    }

    internal IAgentFrameworkWorkspaceService Service { get; }

    public void Dispose() {
        try {
            owner.Dispose();
        } finally {
            services.Dispose();
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> dispatch) where T : class {
        var proxy = DispatchProxy.Create<T, AgentEffectRecoveryDependencyProxy>();
        ((AgentEffectRecoveryDependencyProxy)(object)proxy).Dispatch = dispatch;
        return proxy;
    }
}

public class AgentEffectRecoveryDependencyProxy : DispatchProxy {
    internal Func<MethodInfo, object?[]?, object?> Dispatch { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Dispatch(targetMethod!, args);
}

public interface IAgentEffectRecoverySynchronousWorkspace : IAgentFrameworkWorkspaceService, IAgentFrameworkWorkspaceActivityExecutionService {
}
