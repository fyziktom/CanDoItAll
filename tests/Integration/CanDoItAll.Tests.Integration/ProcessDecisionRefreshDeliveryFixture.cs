using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshJournalFixture;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessDecisionRefreshDeliveryFixture {
    internal static ProcessDecisionRefreshDeliveryCoordinator Delivery(IServiceProvider services, CaptureProbe capture,
        IProcessDecisionRefreshCaptureStore? journal = null, IProcessRuntimeUnitOfWork? owner = null,
        IEnumerable<IProcessDecisionRefreshChildStopper>? stoppers = null) {
        ProcessDecisionRefreshAccountingCoordinator accounting = owner is null ? services.GetRequiredService<ProcessDecisionRefreshAccountingCoordinator>()
            : new(services.GetRequiredService<IProcessAcceptedExecutionInputReader>(), services.GetRequiredService<IProcessDecisionResultQuery>(),
                services.GetRequiredService<IProcessDecisionRefreshReceiptReader>(), services.GetServices<IProcessDecisionChildOutcomeReader>(),
                services.GetRequiredService<IProcessIdempotencyStore>(), owner, NullLogger<ProcessDecisionRefreshAccountingCoordinator>.Instance);
        return new(services.GetRequiredService<IProcessRuntimeStateStore>(), services.GetRequiredService<IProcessDecisionRefreshReceiptReader>(),
            services.GetRequiredService<IProcessDecisionResultQuery>(), services.GetRequiredService<IProcessRootCancellationBarrierReader>(),
            stoppers ?? services.GetServices<IProcessDecisionRefreshChildStopper>(),
            accounting, Coordinator(services, capture, journal, owner), services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>(),
            journal ?? services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(), services.GetRequiredService<TimeProvider>(),
            NullLogger<ProcessDecisionRefreshDeliveryCoordinator>.Instance);
    }

    internal static async Task<ProcessCancellationRecoveryClaim?> CancelUntilAsync(IServiceProvider services,
        ProcessCancellationRecoveryClaim? claim = null, bool pauseAtScope = false) {
        var queue = services.GetRequiredService<IProcessCancellationRecoveryQueue>();
        claim ??= await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, TimeSpan.FromMinutes(2));
        Assert.NotNull(claim);
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
        for (var operation = 0; operation < 64; operation++) {
            if (pauseAtScope && claim.Position.Stage == ProcessCancellationRecoveryStage.SharedScope) {
                return claim;
            }
            await using var scope = services.CreateAsyncScope();
            var checkpoint = await scope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryStep>().AdvanceAsync(claim, deadline.Token);
            checkpoint.Validate(claim);
            Assert.Equal(ProcessCancellationRecoveryPending.None, checkpoint.Pending);
            var saved = await queue.CheckpointAsync(claim, checkpoint, deadline.Token);
            if (checkpoint.Position.Stage == ProcessCancellationRecoveryStage.Completed) {
                Assert.Equal(ProcessCancellationCheckpointStatus.Released, saved.Status);
                return null;
            }
            Assert.Equal(ProcessCancellationCheckpointStatus.Applied, saved.Status);
            claim = saved.Claim!;
        }
        throw new InvalidOperationException("The original cancellation did not reach its bounded test checkpoint.");
    }

    internal sealed class AfterCommit(IProcessRuntimeUnitOfWork inner, Func<ProcessRuntimeCommitRequest, bool> matches,
        bool legacy = false) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            var result = await inner.CommitAsync(legacy ? request with { RefreshCapture = null } : request, cancellationToken);
            if (matches(request)) {
                throw new IOException("The original owner committed before its acknowledgement was lost.");
            }
            return result;
        }
    }

    internal sealed class StopProbe(IProcessDecisionRefreshChildStopper inner,
        Func<ProcessDecisionChildMaterialBinding, RuntimeCommandId, CancellationToken, Task<ProcessDecisionChildStopResult>> stop)
        : IProcessDecisionRefreshChildStopper {
        internal int Calls { get; private set; }
        public ProcessDecisionExecutionContract Contract => inner.Contract;
        public Task<ProcessDecisionChildStopResult> StopAsync(ProcessDecisionChildMaterialBinding original, RuntimeCommandId admission,
            CancellationToken cancellationToken = default) {
            Calls++;
            return stop(original, admission, cancellationToken);
        }
    }
}
