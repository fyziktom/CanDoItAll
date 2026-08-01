using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ContextCapabilityBuilder(string workspaceRoot)
{
    private const string RagStateKeyPrefix = "CanDoItAll.Rag.";
    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);

    public void AddRagProvider(
        RuntimeCapabilityState state,
        CapabilityCatalogItem capability,
        AgentRuntimeConfiguration agentConfiguration)
    {
        var configuration = MafRuntimeJson.DeserializeConfiguration<RagCapabilityConfiguration>(capability.ConfigurationJson) ?? new RagCapabilityConfiguration();
        var ragRoot = MafRuntimePathResolver.ResolvePathFromWorkspace(workspaceRoot, configuration.RagRoot ?? capability.EndpointOrPath, allowExternal: false);
        if (!Directory.Exists(ragRoot) && !File.Exists(ragRoot))
        {
            return;
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
            (query, cancellationToken) => SearchWorkspaceAsync(
                ragRoot,
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

    private async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchWorkspaceAsync(
        string rootPath,
        string query,
        int maxResults,
        int maxFilesToScan,
        int minQueryTerms,
        int minMatchedTerms,
        int minScore,
        HashSet<string>? extensions,
        HashSet<string>? excludedPaths,
        CancellationToken cancellationToken)
    {
        var terms = WorkspaceSearchSupport.TokenizeRagQuery(query);
        if (!WorkspaceSearchSupport.HasEnoughRagSignal(terms, minQueryTerms))
        {
            return [];
        }

        var effectiveMinMatchedTerms = Math.Min(minMatchedTerms, terms.Count);
        var files = WorkspaceSearchSupport.EnumerateSearchFiles(rootPath, extensions, excludedPaths).Take(maxFilesToScan).ToList();
        var scoredResults = new List<(int Score, int MatchedTerms, string Path, string Snippet)>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string text;
            try
            {
                text = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var score = 0;
            var matchedTerms = 0;
            foreach (var term in terms)
            {
                var occurrences = WorkspaceSearchSupport.CountWholeTermOccurrences(text, term);
                if (occurrences <= 0)
                {
                    continue;
                }

                score += occurrences;
                matchedTerms++;
            }

            if (matchedTerms < effectiveMinMatchedTerms || score < minScore)
            {
                continue;
            }

            scoredResults.Add((score, matchedTerms, file, WorkspaceSearchSupport.BuildSearchSnippet(text, terms)));
        }

        return scoredResults
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.MatchedTerms)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(item => new TextSearchProvider.TextSearchResult
            {
                SourceName = Path.GetRelativePath(workspaceRoot, item.Path),
                SourceLink = item.Path,
                Text = item.Snippet
            })
            .ToList();
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
