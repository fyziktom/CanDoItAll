using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

internal sealed record ProjectTaskPlanDispatchFixture(ProjectTaskPlanExecutionEntryTests.EntryFixture Entry,
    ProcessStepMaterialFixture Process, ProjectTaskPlanScenarioClock Clock) : IAsyncDisposable {
    internal IServiceProvider Services => Entry.Files.Sql.Services;

    internal ProcessExecutionAdapterRequest Request {
        get {
            var descriptor = AgentFrameworkProcessExecutionAdapter.DriverDescriptor;
            ProcessStrategyBindingSnapshot binding = new(descriptor.DriverId, descriptor.Strategy.StrategyId,
                descriptor.Strategy.StrategyVersion, "1.0.0", StandardProcessAdapterDriverPackageFactory.MinimumRuntimeSchema,
                StandardProcessAdapterDriverPackageFactory.MaximumRuntimeSchema, "sha256:" + new string('a', 64), []) {
                HostCapabilities = [new(ProcessHostCapabilityIds.ManagedProcessAdapter, ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready, ProcessHostExecutionPort.ManagedProcessAdapter)]
            };
            return new(Process.State.RunId, Process.Claim.StepInstanceId, descriptor.Adapter.Kind,
                new("planning-dispatch-fixture"), binding, [], []) {
                DispatchClaimIdentity = new(Process.Claim.ClaimToken.Value)
            };
        }
    }

    internal static async Task<ProjectTaskPlanDispatchFixture> CreateAsync(ReadBasisMode mode, TimeSpan? claimDuration = null,
        Action<IServiceCollection>? configureServices = null) {
        var clock = new ProjectTaskPlanScenarioClock();
        var entry = await ProjectTaskPlanExecutionEntryTests.EntryFixture.CreateAsync(
            configureServices: services => {
                services.AddSingleton<TimeProvider>(clock);
                configureServices?.Invoke(services);
            });
        try {
            var process = await ProcessStepMaterialFixture.CreateAsync(mode, existingSql: entry.Files.Sql,
                executorId: entry.Files.Agent.Id, allowTaskPublication: true, claimDuration: claimDuration);
            return new(entry, process, clock);
        } catch {
            await entry.DisposeAsync();
            throw;
        }
    }

    internal async Task<ProcessExecutionAdapterResult> ExecuteAsync(ProcessExecutionAdapterRequest? request = null,
        CancellationToken cancellationToken = default) {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IProcessExecutionAdapter>()
            .ExecuteAsync(request ?? Request, cancellationToken);
    }

    internal async Task<StrategyResultEnvelope> ExecuteStrategyAsync(CancellationToken cancellationToken = default) {
        await using var scope = Services.CreateAsyncScope();
        var driver = scope.ServiceProvider.GetRequiredService<IProcessStepExecutionDriver>();
        var request = Request;
        var strategy = await new StandardProcessAdapterStrategyFactory(driver).CreateAsync(request.Binding, cancellationToken);
        return await strategy.ExecuteAsync(new(request.RunId, request.StepId, request.Binding, [], request.StepContract) {
            DispatchClaimIdentity = request.DispatchClaimIdentity
        }, cancellationToken);
    }

    internal async Task<ProcessRuntimeCommitResult> SubmitAsync(StrategyResultEnvelope result) {
        await using var scope = Services.CreateAsyncScope();
        var state = (await scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(Process.State.RunId))!;
        return await scope.ServiceProvider.GetRequiredService<ProcessDecisionResultCoordinator>().SubmitAsync(state,
            new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("planning-dispatch-fixture")),
                new("planning-dispatch-fixture"), Clock.GetUtcNow()),
            new(Process.Claim.StepInstanceId, Process.Claim.OwnerId, Process.Claim.ClaimToken, new(result.IdempotencyKey), result));
    }

    internal async Task<ExecutionRunRecord> ReadAsync(ProcessExecutionRunId execution)
        => (await Entry.Files.Store.GetExecutionRunAsync(execution.Value))!;

    public async ValueTask DisposeAsync() {
        try {
            await Process.DisposeAsync();
        } finally {
            await Entry.DisposeAsync();
        }
    }
}
