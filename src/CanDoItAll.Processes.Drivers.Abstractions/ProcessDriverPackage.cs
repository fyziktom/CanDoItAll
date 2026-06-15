namespace CanDoItAll.Processes.Drivers.Abstractions;

public sealed record ProcessDriverPackage(
    ProcessDriverDescriptor Descriptor,
    IReadOnlyList<IProcessStrategyFactory> StrategyFactories,
    IReadOnlyList<IProcessBranchFamilyProvider> BranchFamilyProviders,
    IReadOnlyList<IProcessRecoveryProvider> RecoveryProviders,
    IReadOnlyList<IProcessResupplyProvider> ResupplyProviders,
    IReadOnlyList<IProcessManagerFacetProvider> ManagerFacetProviders,
    IReadOnlyList<IProcessTemplateFragmentProvider> TemplateFragmentProviders);

public interface IProcessBranchFamilyProvider
{
    IReadOnlyList<BranchFamilyContribution> GetBranchFamilies();
}

public interface IProcessRecoveryProvider
{
    ValueTask<StrategyResultEnvelope> RecoverAsync(
        ProcessStrategyExecutionContext context,
        CancellationToken cancellationToken = default);
}

public interface IProcessResupplyProvider
{
    ValueTask<StrategyResultEnvelope> ResupplyAsync(
        ProcessStrategyExecutionContext context,
        CancellationToken cancellationToken = default);
}

public interface IProcessManagerFacetProvider
{
    IReadOnlyList<ProcessDriverFacetDescriptor> GetFacets();
}

public interface IProcessTemplateFragmentProvider
{
    IReadOnlyList<ProcessTemplateFragmentDescriptor> GetTemplateFragments();
}

public sealed record BranchFamilyContribution(
    DriverFacetKey Key,
    string SchemaVersion,
    string ContentHash);

public sealed record ProcessTemplateFragmentDescriptor(
    ProcessTemplateFragmentKey Key,
    string SchemaVersion,
    string ContentHash);
