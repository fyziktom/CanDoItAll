using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ContextCapabilityBuilder
{
    private const string RagStateKeyPrefix = "CanDoItAll.Rag.";
    private readonly WorkspaceRagRetriever ragRetriever;
    private readonly string workspaceRoot;

    public ContextCapabilityBuilder(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        ragRetriever = new WorkspaceRagRetriever(this.workspaceRoot, workspaceScope);
    }

    public bool AddRagProvider(
        RuntimeCapabilityState state,
        CapabilityCatalogItem capability,
        AgentRuntimeConfiguration agentConfiguration)
    {
        var configuration = MafRuntimeJson.DeserializeConfiguration<RagCapabilityConfiguration>(capability.ConfigurationJson) ?? new RagCapabilityConfiguration();
        var ragRoot = MafRuntimePathResolver.ResolvePathFromWorkspace(workspaceRoot, configuration.RagRoot ?? capability.EndpointOrPath, allowExternal: false);
        if (!Directory.Exists(ragRoot) && !File.Exists(ragRoot))
        {
            return false;
        }

        var searchRoots = ragRetriever.ResolveSearchRoots(ragRoot);
        if (searchRoots.Count == 0)
        {
            return false;
        }

        var searchTime = Enum.TryParse<TextSearchProviderOptions.TextSearchBehavior>(
            configuration.SearchTime,
            ignoreCase: true,
            out var parsedBehavior)
            ? parsedBehavior
            : TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke;

        var recentMessageMemoryLimit = configuration.RecentMessageMemoryLimit ?? 4;
        var maxResults = configuration.MaxResults ?? agentConfiguration.MaxLocalRagResults ?? 4;
        var maxFilesToScan = Math.Clamp(configuration.MaxFilesToScan ?? 256, 1, 4096);
        var minQueryTerms = Math.Clamp(configuration.MinQueryTerms ?? 2, 1, 12);
        var minMatchedTerms = Math.Clamp(configuration.MinMatchedTerms ?? 2, 1, 12);
        var minScore = Math.Clamp(configuration.MinScore ?? 2, 1, 100);
        var extensions = configuration.Extensions?.Where(item => !string.IsNullOrWhiteSpace(item)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var excludedPaths = configuration.ExcludePaths?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(WorkspaceSearchSupport.NormalizeSearchPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        state.ContextProviders.Add(new TextSearchProvider(
            (query, cancellationToken) => ragRetriever.SearchAsync(
                searchRoots,
                query,
                maxResults,
                maxFilesToScan,
                minQueryTerms,
                minMatchedTerms,
                minScore,
                extensions,
                excludedPaths,
                cancellationToken),
            new TextSearchProviderOptions
            {
                SearchTime = searchTime,
                RecentMessageMemoryLimit = recentMessageMemoryLimit,
                StateKey = $"{RagStateKeyPrefix}{capability.Id:N}"
            }));

        return true;
    }

    public void AddConfiguredAiContextProvider(
        RuntimeCapabilityState state,
        CapabilityCatalogItem capability)
    {
        var configuration = MafRuntimeJson.DeserializeConfiguration<AiContextCapabilityConfiguration>(capability.ConfigurationJson);
        if (string.IsNullOrWhiteSpace(configuration?.Message))
        {
            return;
        }

        var role = MafRuntimeChatRoles.Parse(configuration.Role);
        state.ContextProviders.Add(new StaticMessageContextProvider(
            new ChatMessage(role, configuration.Message),
            StaticMessageContextProvider.CreateCapabilityStateKey(capability.Id)));
    }

}

internal sealed class StaticMessageContextProvider : MessageAIContextProvider
{
    private const string StateKeyPrefix = "CanDoItAll.StaticContext.";
    public const string TransientAgentChatStateKey = $"{StateKeyPrefix}TransientAgentChat";
    public const string EffectiveExternalTargetsStateKey = $"{StateKeyPrefix}EffectiveExternalTargets";
    private readonly ChatMessage message;
    private readonly string stateKey;

    public StaticMessageContextProvider(ChatMessage message, string stateKey)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateKey);
        this.message = message;
        this.stateKey = stateKey.Trim();
    }

    public override IReadOnlyList<string> StateKeys => [stateKey];

    public static string CreateCapabilityStateKey(Guid capabilityId)
    {
        if (capabilityId == Guid.Empty)
        {
            throw new ArgumentException("A context capability id is required.", nameof(capabilityId));
        }

        return $"{StateKeyPrefix}Capability.{capabilityId:N}";
    }

    protected override ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        return new ValueTask<IEnumerable<ChatMessage>>([message]);
    }
}
