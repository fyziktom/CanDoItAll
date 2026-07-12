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
            adapter.Strategy.StrategyId,
            adapter.Strategy.StrategyVersion,
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
