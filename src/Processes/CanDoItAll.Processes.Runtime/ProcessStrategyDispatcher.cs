using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed class ProcessStrategyDispatcher(
    IProcessHostCapabilitySnapshotProvider? hostCapabilitySnapshotProvider = null)
{
    public async Task<StrategyResultEnvelope> InvokeAsync(
        DispatchWorkItem workItem,
        ProcessInstancePlan plan,
        IProcessStrategyFactory strategyFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(strategyFactory);

        if (workItem.DispatchClaimIdentity is not { } dispatchClaimIdentity)
        {
            throw new InvalidOperationException(
                "Dispatch work item must be bound to an active claim before strategy invocation.");
        }

        var planStep = FindPlanStep(plan, workItem.StepInstanceId);
        if (planStep?.ExecutionStrategyBinding is null)
        {
            throw new InvalidOperationException("Dispatch work item must reference a planned executable step with a strategy binding.");
        }

        if (planStep.StepDefinitionId != workItem.StepDefinitionId)
        {
            throw new InvalidOperationException(
                "Dispatch step definition identity does not match the immutable plan.");
        }

        if (!HasMatchingStepHostCapabilityContract(
                planStep.RequiredHostCapabilities,
                workItem.StepContract.RequiredHostCapabilities))
        {
            throw new InvalidOperationException(
                "Dispatch step host capability contract does not match the immutable plan.");
        }

        if (!HasMatchingRuntimeToolContract(
                planStep.RequiredRuntimeToolNames,
                workItem.StepContract.RequiredRuntimeToolNames))
        {
            throw new InvalidOperationException(
                "Dispatch step runtime-tool contract does not match the immutable plan.");
        }

        if (!HasMatchingStrategyBinding(planStep.ExecutionStrategyBinding, workItem.StrategyBinding) ||
            strategyFactory.Descriptor.StrategyId != workItem.StrategyBinding.StrategyId ||
            !string.Equals(
                strategyFactory.Descriptor.StrategyVersion,
                workItem.StrategyBinding.StrategyVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Dispatcher strategy binding does not match the immutable plan.");
        }

        if (!HasMatchingHostCapabilityContract(
                workItem.StrategyBinding,
                strategyFactory.Descriptor.RequiredHostCapabilities))
        {
            throw new InvalidOperationException(
                "Dispatcher strategy host capability contract does not match the immutable binding.");
        }

        var hostGate = await EvaluateHostCapabilitiesAsync(
                workItem.StepContract.RequiredHostCapabilities,
                cancellationToken)
            .ConfigureAwait(false);
        if (!hostGate.IsSatisfied)
        {
            return CreateHostCapabilityFailureResult(workItem, hostGate.Evidence);
        }

        var strategy = await strategyFactory.CreateAsync(workItem.StrategyBinding, cancellationToken).ConfigureAwait(false);
        var context = new ProcessStrategyExecutionContext(
            workItem.RunId,
            workItem.StepInstanceId,
            workItem.StrategyBinding,
            workItem.StrategyBinding.Inputs,
            workItem.StepContract)
        {
            DispatchClaimIdentity = dispatchClaimIdentity,
            DispatchHostCapabilityEvidence = hostGate.Evidence
        };

        var result = await strategy.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        if (result is null ||
            result.StrategyId != workItem.StrategyBinding.StrategyId ||
            !string.Equals(
                result.StrategyVersion,
                workItem.StrategyBinding.StrategyVersion,
                StringComparison.Ordinal) ||
            !ProcessStrategyReceiptValuePolicy.IsStableIdentifier(result.StrategyId.Value) ||
            !ProcessStrategyReceiptValuePolicy.IsStableVersion(result.StrategyVersion))
        {
            throw new InvalidOperationException(
                "Strategy result identity does not match the immutable dispatch binding.");
        }

        if (!ProcessHostCapabilityEvidencePolicy.TryMerge(
                hostGate.Evidence,
                result.HostCapabilityEvidence,
                out var mergedEvidence))
        {
            return CreateHostCapabilityChangedResult(
                workItem,
                hostGate.Evidence,
                result.HostCapabilityEvidence);
        }

        return result with { HostCapabilityEvidence = mergedEvidence };
    }

    private async ValueTask<HostCapabilityGateResult> EvaluateHostCapabilitiesAsync(
        IReadOnlySet<ProcessHostCapabilityId> requiredCapabilities,
        CancellationToken cancellationToken)
    {
        if (requiredCapabilities is not { Count: > 0 })
        {
            return new HostCapabilityGateResult(true, null);
        }

        if (requiredCapabilities.Count > ProcessHostCapabilitySnapshot.MaximumCapabilities)
        {
            return new HostCapabilityGateResult(
                false,
                new ProcessHostCapabilityEvaluationEvidence(new ProcessHostProfileId("unknown"), []));
        }

        ProcessHostCapabilitySnapshot? snapshot = null;
        var probeFailed = hostCapabilitySnapshotProvider is null;
        if (!probeFailed)
        {
            try
            {
                snapshot = await hostCapabilitySnapshotProvider!
                    .GetAsync(cancellationToken)
                    .ConfigureAwait(false);
                probeFailed = snapshot is null || !snapshot.IsStructurallyValid();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                probeFailed = true;
            }
        }

        var profileId = probeFailed
            ? new ProcessHostProfileId("unknown")
            : snapshot!.ProfileId;
        var facts = new List<ProcessHostCapabilityFact>(requiredCapabilities.Count);
        foreach (var capabilityId in requiredCapabilities.OrderBy(id => id.Value, StringComparer.Ordinal))
        {
            if (!probeFailed && snapshot!.TryGet(capabilityId, out var reportedFact))
            {
                facts.Add(reportedFact!);
                continue;
            }

            facts.Add(new ProcessHostCapabilityFact(
                capabilityId,
                ProcessHostCapabilityAvailability.Unavailable,
                probeFailed
                    ? ProcessHostCapabilityReason.ProbePending
                    : ProcessHostCapabilityReason.NotRegistered,
                ProcessHostExecutionPort.None));
        }

        var evidence = new ProcessHostCapabilityEvaluationEvidence(profileId, facts);
        return new HostCapabilityGateResult(facts.All(fact => fact.IsAvailable), evidence);
    }

    private static bool HasMatchingHostCapabilityContract(
        ProcessStrategyBindingSnapshot binding,
        IReadOnlySet<ProcessHostCapabilityId> requiredCapabilities)
    {
        if (requiredCapabilities is null ||
            requiredCapabilities.Count > ProcessHostCapabilitySnapshot.MaximumCapabilities)
        {
            return false;
        }

        var snapshot = new ProcessHostCapabilitySnapshot(
            binding.HostProfileId,
            binding.HostCapabilities);
        return snapshot.IsStructurallyValid() &&
               requiredCapabilities.SetEquals(binding.HostCapabilities.Select(fact => fact.Id));
    }

    private static bool HasMatchingStrategyBinding(
        ProcessStrategyBindingSnapshot plannedBinding,
        ProcessStrategyBindingSnapshot runtimeBinding)
    {
        if (plannedBinding.DriverId != runtimeBinding.DriverId ||
            plannedBinding.StrategyId != runtimeBinding.StrategyId ||
            !string.Equals(plannedBinding.StrategyVersion, runtimeBinding.StrategyVersion, StringComparison.Ordinal) ||
            !string.Equals(plannedBinding.FactoryVersion, runtimeBinding.FactoryVersion, StringComparison.Ordinal) ||
            !string.Equals(plannedBinding.MinRuntimeSchema, runtimeBinding.MinRuntimeSchema, StringComparison.Ordinal) ||
            !string.Equals(plannedBinding.MaxRuntimeSchema, runtimeBinding.MaxRuntimeSchema, StringComparison.Ordinal) ||
            !string.Equals(plannedBinding.BindingInputsHash, runtimeBinding.BindingInputsHash, StringComparison.Ordinal) ||
            plannedBinding.HostProfileId != runtimeBinding.HostProfileId ||
            plannedBinding.Inputs.Count != runtimeBinding.Inputs.Count ||
            plannedBinding.HostCapabilities.Count != runtimeBinding.HostCapabilities.Count)
        {
            return false;
        }

        if (!plannedBinding.Inputs.SequenceEqual(runtimeBinding.Inputs))
        {
            return false;
        }

        var plannedFacts = plannedBinding.HostCapabilities
            .OrderBy(fact => fact.Id.Value, StringComparer.Ordinal)
            .ToArray();
        var runtimeFacts = runtimeBinding.HostCapabilities
            .OrderBy(fact => fact.Id.Value, StringComparer.Ordinal)
            .ToArray();
        return plannedFacts.SequenceEqual(runtimeFacts);
    }

    private static bool HasMatchingStepHostCapabilityContract(
        IReadOnlySet<ProcessHostCapabilityId> plannedCapabilities,
        IReadOnlySet<ProcessHostCapabilityId> runtimeCapabilities)
        => plannedCapabilities is not null &&
           runtimeCapabilities is not null &&
           plannedCapabilities.Count <= ProcessHostCapabilitySnapshot.MaximumCapabilities &&
           runtimeCapabilities.Count <= ProcessHostCapabilitySnapshot.MaximumCapabilities &&
           plannedCapabilities.SetEquals(runtimeCapabilities);

    private static bool HasMatchingRuntimeToolContract(
        IReadOnlyList<string> plannedToolNames,
        IReadOnlyList<string> runtimeToolNames)
        => ProcessRequiredRuntimeToolNames.IsValidBoundedContract(plannedToolNames) &&
           ProcessRequiredRuntimeToolNames.IsValidBoundedContract(runtimeToolNames) &&
           plannedToolNames.Count == runtimeToolNames.Count &&
           plannedToolNames.SequenceEqual(runtimeToolNames, StringComparer.OrdinalIgnoreCase);

    private static StrategyResultEnvelope CreateHostCapabilityFailureResult(
        DispatchWorkItem workItem,
        ProcessHostCapabilityEvaluationEvidence? evidence)
    {
        const string summary = "Required process host capabilities are unavailable. Retry after the host configuration is ready.";
        var evidenceKey = evidence is null
            ? "invalid-contract"
            : string.Join(
                '|',
                evidence.Capabilities.Select(fact =>
                    $"{fact.Id.Value}:{fact.Availability}:{fact.Reason}:{fact.ExecutionPort}"));
        var stableKey = $"host-capability-gate:{workItem.RunId}:{workItem.StepInstanceId}:{evidence?.ProfileId.Value ?? "unknown"}:{evidenceKey}";
        var hash = ComputeHash(stableKey);

        return new StrategyResultEnvelope(
            workItem.StrategyBinding.StrategyId,
            workItem.StrategyBinding.StrategyVersion,
            CreateDeterministicGuid(stableKey),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.runtime.host_capability_unavailable"),
                    StrategyDiagnosticSensitivity.Normal,
                    hash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.runtime.host_capability_unavailable"),
                    hash,
                    summary)
            ],
            hash)
        {
            UserSafeSummary = summary,
            HostCapabilityEvidence = evidence
        };
    }

    private static StrategyResultEnvelope CreateHostCapabilityChangedResult(
        DispatchWorkItem workItem,
        ProcessHostCapabilityEvaluationEvidence? dispatchEvidence,
        ProcessHostCapabilityEvaluationEvidence? strategyEvidence)
    {
        const string summary = "Process host capability facts changed during dispatch. Retry after the host configuration is stable.";
        var evidence = ProcessHostCapabilityEvidencePolicy.CreateUnstableEvidence(
            dispatchEvidence,
            strategyEvidence);
        var capabilityIds = evidence.Capabilities.Select(fact => fact.Id).ToArray();
        var stableKey = $"host-capability-changed:{workItem.RunId}:{workItem.StepInstanceId}:{string.Join('|', capabilityIds.Select(id => id.Value))}";
        var hash = ComputeHash(stableKey);

        return new StrategyResultEnvelope(
            workItem.StrategyBinding.StrategyId,
            workItem.StrategyBinding.StrategyVersion,
            CreateDeterministicGuid(stableKey),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.runtime.host_capability_snapshot_changed"),
                    StrategyDiagnosticSensitivity.Normal,
                    hash,
                    summary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.runtime.host_capability_snapshot_changed"),
                    hash,
                    summary)
            ],
            hash)
        {
            UserSafeSummary = summary,
            HostCapabilityEvidence = evidence
        };
    }

    private static string ComputeHash(string value)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static Guid CreateDeterministicGuid(string value)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private static StepInstancePlan? FindPlanStep(ProcessInstancePlan plan, ProcessStepInstanceId stepId)
    {
        foreach (var step in plan.Steps)
        {
            if (step.StepInstanceId == stepId)
            {
                return step;
            }
        }

        return null;
    }

    private sealed record HostCapabilityGateResult(
        bool IsSatisfied,
        ProcessHostCapabilityEvaluationEvidence? Evidence);
}
