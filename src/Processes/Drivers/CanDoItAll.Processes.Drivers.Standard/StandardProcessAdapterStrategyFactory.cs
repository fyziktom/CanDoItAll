using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Drivers.Standard;

public sealed class StandardProcessAdapterStrategyFactory(IProcessStepExecutionDriver driver) : IProcessStrategyFactory
{
    public ProcessStrategyDescriptor Descriptor => driver.Descriptor.Strategy;

    public ValueTask<IProcessStrategy> CreateAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (binding.StrategyId != Descriptor.StrategyId)
        {
            throw new InvalidOperationException(
                $"Strategy binding '{binding.StrategyId}' does not match adapter driver strategy '{Descriptor.StrategyId}'.");
        }

        return ValueTask.FromResult<IProcessStrategy>(new StandardProcessAdapterStrategy(driver));
    }
}

internal sealed class StandardProcessAdapterStrategy(IProcessStepExecutionDriver driver) : IProcessStrategy
{
    public async ValueTask<StrategyResultEnvelope> ExecuteAsync(
        ProcessStrategyExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var adapter = driver.Descriptor.Adapter;
        var result = await driver.ExecuteStepAsync(
            new ProcessExecutionAdapterRequest(
                context.RunId,
                context.StepId,
                adapter.Kind,
                new ProcessExecutionAdapterOperationKey($"{adapter.AdapterId}.execute"),
                context.Binding,
                context.Inputs,
                CreateContextFacets(context))
            {
                StepContract = context.StepContract,
                DispatchClaimIdentity = context.DispatchClaimIdentity
            },
            cancellationToken);

        var diagnostics = new List<StrategyDiagnosticRef>(result.Diagnostics.Count);
        foreach (var diagnostic in result.Diagnostics)
        {
            diagnostics.Add(new StrategyDiagnosticRef(
                diagnostic.Code,
                diagnostic.Sensitivity,
                diagnostic.EvidenceHash,
                diagnostic.SafeSummary,
                diagnostic.RestrictedEvidenceReference,
                diagnostic.RetrySafety,
                diagnostic.Idempotency)
            {
                RelatedChildRunId = diagnostic.RelatedChildRunId,
                ExecutionSafetyAttestation = diagnostic.ExecutionSafetyAttestation
            });
        }

        return new StrategyResultEnvelope(
            adapter.Strategy.StrategyId,
            adapter.Strategy.StrategyVersion,
            Guid.NewGuid(),
            result.Outcome,
            result.ProducedArtifacts,
            result.RequestedArtifacts,
            diagnostics,
            result.ManagerSignals,
            result.ResultHash)
        {
            UserSafeSummary = result.UserSafeSummary,
            ExecutionRunId = result.ExecutionRunId
        };
    }

    private static IReadOnlyList<ProcessExecutionContextFacet> CreateContextFacets(
        ProcessStrategyExecutionContext context)
    {
        var facets = new List<ProcessExecutionContextFacet>(context.Inputs.Count + 4);
        foreach (var input in context.Inputs)
        {
            facets.Add(new ProcessExecutionContextFacet(
                new ProcessExecutionContextFacetKey(input.Key.Value),
                input.ValueHash,
                StrategyDiagnosticSensitivity.Normal));
        }

        facets.Add(new ProcessExecutionContextFacet(
            new ProcessExecutionContextFacetKey("process.step.contract"),
            context.StepContract.ContractHash,
            StrategyDiagnosticSensitivity.Normal));
        facets.Add(new ProcessExecutionContextFacet(
            new ProcessExecutionContextFacetKey("process.step.required-artifacts"),
            HashFacetValues(context.StepContract.RequiredArtifacts.Select(artifact => artifact.ConnectionHash)),
            StrategyDiagnosticSensitivity.Normal));
        facets.Add(new ProcessExecutionContextFacet(
            new ProcessExecutionContextFacetKey("process.step.expected-outputs"),
            HashFacetValues(context.StepContract.ExpectedProducedArtifacts.Select(artifact => artifact.SlotId.Value.ToString("N"))),
            StrategyDiagnosticSensitivity.Normal));
        facets.Add(new ProcessExecutionContextFacet(
            new ProcessExecutionContextFacetKey("process.step.required-runtime-tools"),
            HashFacetValues(context.StepContract.RequiredRuntimeToolNames),
            StrategyDiagnosticSensitivity.Normal));

        return facets;
    }

    private static string HashFacetValues(IEnumerable<string> values)
    {
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            return "sha256:empty";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(';', normalized)));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
