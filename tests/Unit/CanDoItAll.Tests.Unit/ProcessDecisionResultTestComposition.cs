using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal static class ProcessDecisionResultTestComposition {
    internal static ProcessDecisionResultCoordinator Legacy(IProcessRuntimeUnitOfWork unitOfWork)
        => new(new LegacySelection(), [], new NoGovernedReceipts(), unitOfWork, NullLogger<ProcessDecisionResultCoordinator>.Instance);

    internal static ProcessDecisionRootPreparationCoordinator LegacyRoot(IProcessRuntimeStateStore states, IProcessRuntimeUnitOfWork unitOfWork)
        => new(new LegacyRootSelection(), [], states, new NoGovernedReceipts(), unitOfWork,
            TimeProvider.System, NullLogger<ProcessDecisionRootPreparationCoordinator>.Instance);

    internal static ProcessDecisionRootPreparationCoordinator RootSourceRequired(IProcessRuntimeStateStore states, IProcessRuntimeUnitOfWork unitOfWork)
        => new(new MissingRootSource(), [], states, new NoGovernedReceipts(), unitOfWork,
            TimeProvider.System, NullLogger<ProcessDecisionRootPreparationCoordinator>.Instance);

    private sealed class MissingRootSource : IProcessDecisionRootQuery {
        public Task<ProcessDecisionRootSelection?> ReadAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken = default)
            => throw new ProcessDecisionSourceProjectionRequiredException();
    }

    private sealed class LegacyRootSelection : IProcessDecisionRootQuery {
        public Task<ProcessDecisionRootSelection?> ReadAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessDecisionRootSelection?>(null);
    }

    private sealed class LegacySelection : IProcessDecisionResultQuery {
        public Task<ProcessDecisionResultSelection?> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
            ProcessExecutionRunId? executionRunId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProcessDecisionResultSelection?>(null);
    }

    private sealed class NoGovernedReceipts : IProcessIdempotencyStore {
        public Task<ProcessRuntimeCommitResult?> FindCompletedCommandAsync(RuntimeCommandId commandId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A legacy result must not query governed command receipts.");
    }
}
