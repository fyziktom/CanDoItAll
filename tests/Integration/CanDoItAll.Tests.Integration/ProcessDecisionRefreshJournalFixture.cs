using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessDecisionRefreshJournalFixture {
    internal static async Task<(RuntimeCommandId Admission, ProcessRuntimeStateSnapshot State)> StartAsync(ProjectTaskPlanReviewFixture fixture) {
        var admission = await ProcessDecisionRefreshCaptureFixture.AdmitAsync(fixture);
        return (admission, await ProcessDecisionRefreshCaptureFixture.AccountAsync(fixture, admission));
    }

    internal static ProcessDecisionRefreshCaptureCoordinator Coordinator(IServiceProvider services, CaptureProbe capturer,
        IProcessDecisionRefreshCaptureStore? journal = null, IProcessRuntimeUnitOfWork? owner = null)
        => new(services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>(), journal ?? services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(),
            services.GetRequiredService<IProcessDecisionRootQuery>(), services.GetRequiredService<IProcessDecisionResultQuery>(), [capturer],
            services.GetRequiredService<IProcessRuntimeStateStore>(), owner ?? services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            services.GetRequiredService<TimeProvider>(), NullLogger<ProcessDecisionRefreshCaptureCoordinator>.Instance);

    internal static async Task<ProcessDecisionRefreshCaptureState> SavedAsync(IServiceProvider services, ProcessRuntimeStateSnapshot state,
        RuntimeCommandId admission, ReadBasisMode mode) {
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        return (await services.GetRequiredService<IProcessDecisionRefreshCaptureStore>().ReadAsync(state.RunId,
            mode == ReadBasisMode.RunSnapshot ? identity.RootReserve : identity.LocalReserve))!;
    }

    internal static async Task<string> SnapshotAsync(IServiceProvider services, ProcessRuntimeStateSnapshot state) {
        await using var context = Context(services);
        return JsonSerializer.Serialize(new {
            State = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId),
            Budgets = await context.DecisionBudgets.AsNoTracking().OrderBy(row => row.BudgetId).ToArrayAsync(),
            Allocations = await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(row => row.BudgetId).ThenBy(row => row.AllocationId).ToArrayAsync(),
            Captures = await context.RefreshCaptures.AsNoTracking().OrderBy(row => row.ReservationCommandId).ToArrayAsync(),
            Payloads = await context.RefreshCapturePayloads.CountAsync(), Commands = await context.IdempotencyKeys.CountAsync(),
            Materials = await context.DecisionMaterials.CountAsync(), Events = await context.RuntimeEvents.CountAsync()
        });
    }

    internal static async Task CancelAsync(IServiceProvider services, ProcessRuntimeStateSnapshot state, RuntimeCommandContext command) {
        var current = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId))!;
        Assert.True((await new ProcessRuntimeEngine(services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(current, command)).CommittedNow);
    }

    internal sealed class CaptureProbe(IServiceProvider services,
        Func<ProcessDecisionMaterialCaptureRequest, CancellationToken, Task<ProcessDecisionMaterialCaptureResult>>? capture = null) : IProcessDecisionMaterialCapturer {
        private readonly IProcessDecisionMaterialCapturer actual = services.GetServices<IProcessDecisionMaterialCapturer>()
            .Single(item => item.ContentContract == ProjectTaskPlanProcessContract.Content);
        private int calls;
        internal int Calls => calls;
        public ProcessDecisionContentContract ContentContract => actual.ContentContract;
        public Task<ProcessDecisionMaterialCaptureResult> CaptureAsync(ProcessDecisionMaterialCaptureRequest request, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref calls);
            return capture is null ? actual.CaptureAsync(request, cancellationToken) : capture(request, cancellationToken);
        }
    }

    internal enum Interruption { BeforeBegin, AfterBegin, AfterReturn }

    internal sealed class InterruptedStore(IProcessDecisionRefreshCaptureStore inner, Interruption point,
        Func<Task>? before = null) : IProcessDecisionRefreshCaptureStore {
        public Task<ProcessDecisionRefreshCaptureState?> ReadAsync(ProcessRunId run, RuntimeCommandId reservation, CancellationToken cancellationToken = default)
            => inner.ReadAsync(run, reservation, cancellationToken);
        public Task<ProcessDecisionRefreshCaptureReturned?> ReadReturnedAsync(ProcessRunId run, RuntimeCommandId reservation, CancellationToken cancellationToken = default)
            => inner.ReadReturnedAsync(run, reservation, cancellationToken);
        public async Task<ProcessDecisionRefreshCaptureInvocationResult> BeginAsync(ProcessDecisionRefreshCaptureReservation reservation,
            ProcessDecisionCaptureInvocationId invocation, CancellationToken cancellationToken = default) {
            if (before is not null) {
                await before();
            }
            if (point == Interruption.BeforeBegin) {
                throw new IOException("Capture invocation was interrupted before admission.");
            }
            var result = await inner.BeginAsync(reservation, invocation, cancellationToken);
            if (point == Interruption.AfterBegin) {
                throw new IOException("Capture invocation acknowledgement was lost.");
            }
            return result;
        }
        public async Task<ProcessDecisionRefreshCaptureState> ReturnAsync(ProcessDecisionRefreshCaptureReservation reservation,
            ProcessDecisionCaptureInvocationId invocation, ProcessDecisionRefreshCaptureReturned returned, CancellationToken cancellationToken = default) {
            var result = await inner.ReturnAsync(reservation, invocation, returned, cancellationToken);
            if (point == Interruption.AfterReturn) {
                throw new IOException("Capture return acknowledgement was lost.");
            }
            return result;
        }
        public Task<ProcessDecisionRefreshCaptureState> CloseNeverInvokedAsync(ProcessDecisionRefreshCaptureReservation reservation, CancellationToken cancellationToken = default)
            => inner.CloseNeverInvokedAsync(reservation, cancellationToken);
    }
}
