using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class RuntimeCapabilityState
{
    public List<AITool> Tools { get; } = [];

    public List<AgentRuntimeToolProviderDescriptor> RuntimeToolProviderDescriptors { get; } = [];

    public List<AgentRuntimeToolMetadata> RuntimeToolMetadata { get; } = [];

    public List<AIContextProvider> ContextProviders { get; } = [];

    public AgentContextContributionTraceCollector ContextContributionTraceCollector { get; } = new();

    public List<AgentRuntimeContextManifestSource> ContextSources { get; } = [];

    public HashSet<string> FrameworkToolNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<CapabilityExposureDescriptor> EffectiveCapabilityDescriptors { get; } = [];

    public List<SuppressedCapabilityDiagnostic> CapabilityAccessDiagnostics { get; } = [];

    public EffectiveCapabilitySet EffectiveCapabilities => new(
        EffectiveCapabilityDescriptors,
        CapabilityAccessDiagnostics);

    public List<IAsyncDisposable> AsyncDisposables { get; } = [];

    public List<IDisposable> Disposables { get; } = [];

    public bool HasApprovalTools { get; set; }

    public async Task<IReadOnlyList<Exception>> DisposeAcquiredResourcesAsync()
    {
        var failures = new List<Exception>();
        for (var index = AsyncDisposables.Count - 1; index >= 0; index--)
        {
            try
            {
                await AsyncDisposables[index].DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        AsyncDisposables.Clear();
        for (var index = Disposables.Count - 1; index >= 0; index--)
        {
            try
            {
                Disposables[index].Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        Disposables.Clear();
        return failures;
    }
}

internal sealed record RuntimeCapabilityAccessPlan(
    EffectiveCapabilitySet EffectiveCapabilities,
    IReadOnlyList<CapabilityCatalogItem> AllowedCatalogCapabilities,
    IReadOnlyList<CapabilityAccessPolicy> Policies,
    ICapabilityAccessPolicyEvaluator Evaluator,
    IReadOnlyList<CapabilityExposureDescriptor> InitialAllowedCapabilities,
    IReadOnlyList<SuppressedCapabilityDiagnostic> InitialDiagnostics,
    IReadOnlyDictionary<CapabilityIdentity, CapabilityCatalogItem> CatalogCapabilitiesByIdentity,
    IReadOnlyDictionary<string, CapabilityExposureDescriptor> DescriptorsByKey,
    string CorrelationId);

internal sealed record RuntimeToolProviderRegistration(
    IAgentRuntimeToolProvider Provider,
    AgentRuntimeToolProviderDescriptor Descriptor);

internal sealed record RuntimeToolProviderAttachmentRequest(
    RuntimeCapabilityState State,
    RuntimeCapabilityAccessPlan AccessPlan,
    IReadOnlyList<RuntimeToolProviderRegistration> RuntimeToolProviders,
    AgentRuntimeToolProviderContext Context,
    bool SuppressApprovalRequirements);

internal sealed record RuntimeToolProviderAttachmentResult(
    int AttachedToolCount,
    int RegisteredProviderCount,
    IReadOnlyList<RuntimeToolProviderAttachmentSummary> AttachmentSummaries,
    string ProgressMessage);

internal sealed record RuntimeToolProviderAttachmentSummary(
    string ProviderKey,
    string ProviderName,
    int ToolCount,
    int ExcludedToolCount);

internal sealed record RuntimeToolProviderAccessFilterRequest(
    RuntimeToolProviderRegistration Registration,
    RuntimeCapabilityState State,
    RuntimeCapabilityAccessPlan AccessPlan,
    IReadOnlyList<AITool> Tools,
    IReadOnlyList<AgentRuntimeToolMetadata> Metadata);

internal sealed record FilteredRuntimeToolProviderTools(
    IReadOnlyList<AITool> Tools,
    IReadOnlyList<AgentRuntimeToolMetadata> Metadata,
    int ExcludedToolCount);

internal sealed record MafRuntimeCompositionMeasurement(
    string Stage,
    TimeSpan Elapsed,
    string Source);
