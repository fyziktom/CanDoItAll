using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Drivers.Standard;

public sealed class StandardProcessAdapterStrategyFactory(IProcessExecutionAdapter adapter) : IProcessStrategyFactory
{
    public ProcessStrategyDescriptor Descriptor => adapter.Descriptor.Strategy;

    public ValueTask<IProcessStrategy> CreateAsync(
        ProcessStrategyBindingSnapshot binding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (binding.StrategyId != Descriptor.StrategyId)
        {
            throw new InvalidOperationException(
                $"Strategy binding '{binding.StrategyId}' does not match adapter strategy '{Descriptor.StrategyId}'.");
        }

        return ValueTask.FromResult<IProcessStrategy>(new StandardProcessAdapterStrategy(adapter));
    }
}

internal sealed class StandardProcessAdapterStrategy(IProcessExecutionAdapter adapter) : IProcessStrategy
{
    public async ValueTask<StrategyResultEnvelope> ExecuteAsync(
        ProcessStrategyExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await adapter.ExecuteAsync(
            new ProcessExecutionAdapterRequest(
                context.RunId,
                context.StepId,
                adapter.Descriptor.Kind,
                new ProcessExecutionAdapterOperationKey($"{adapter.Descriptor.AdapterId}.execute"),
                context.Binding,
                context.Inputs,
                CreateContextFacets(context.Inputs)),
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
                diagnostic.Idempotency));
        }

        return new StrategyResultEnvelope(
            adapter.Descriptor.Strategy.StrategyId,
            adapter.Descriptor.Strategy.StrategyVersion,
            Guid.NewGuid(),
            result.Outcome,
            result.ProducedArtifacts,
            result.RequestedArtifacts,
            diagnostics,
            result.ManagerSignals,
            result.ResultHash);
    }

    private static IReadOnlyList<ProcessExecutionContextFacet> CreateContextFacets(
        IReadOnlyList<StrategyBindingInput> inputs)
    {
        var facets = new List<ProcessExecutionContextFacet>(inputs.Count);
        foreach (var input in inputs)
        {
            facets.Add(new ProcessExecutionContextFacet(
                new ProcessExecutionContextFacetKey(input.Key.Value),
                input.ValueHash,
                StrategyDiagnosticSensitivity.Normal));
        }

        return facets;
    }
}
