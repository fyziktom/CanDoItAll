using System.Net.Http.Headers;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Mem0;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ContextCapabilityBuilder(string workspaceRoot)
{
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
                RecentMessageMemoryLimit = recentMessageMemoryLimit
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
        state.ContextProviders.Add(new StaticMessageContextProvider(new ChatMessage(role, configuration.Message)));
    }

    public async Task AddMemoryProviderAsync(
        RuntimeCapabilityState state,
        CapabilityCatalogItem capability,
        AgentDefinition agent,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        var configuration = MafRuntimeJson.DeserializeConfiguration<MemoryCapabilityConfiguration>(capability.ConfigurationJson) ?? new MemoryCapabilityConfiguration();
        if (!string.Equals(configuration.Provider, "mem0", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var endpoint = !string.IsNullOrWhiteSpace(configuration.Endpoint)
            ? configuration.Endpoint
            : capability.EndpointOrPath;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new InvalidOperationException($"Capability '{capability.Name}' does not contain a valid Mem0 endpoint.");
        }

        var apiKey = string.IsNullOrWhiteSpace(configuration.ApiKeyEnvironmentVariable)
            ? string.Empty
            : AgentProviderEnvironmentCredential.ResolveAndPromote(configuration.ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Capability '{capability.Name}' requires environment variable '{configuration.ApiKeyEnvironmentVariable}' for Mem0.");
        }

        var httpClient = new HttpClient
        {
            BaseAddress = endpointUri
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

        var options = BuildMem0Options(configuration, agent);
        var mem0Provider = new Mem0Provider(httpClient, options);

        state.ContextProviders.Add(mem0Provider);
        state.Disposables.Add(httpClient);
        await progressCallback(
            ExecutionState.Preparing,
            "Memory",
            $"Attached Mem0Provider for capability '{capability.Name}' using application scope '{options.ApplicationId ?? "(none)"}'.");
    }

    private static Mem0ProviderOptions BuildMem0Options(MemoryCapabilityConfiguration configuration, AgentDefinition agent)
    {
        var options = new Mem0ProviderOptions
        {
            ApplicationId = ReplaceMemoryPlaceholders(configuration.ApplicationId, agent),
            AgentId = ReplaceMemoryPlaceholders(configuration.AgentId, agent),
            ThreadId = ReplaceMemoryPlaceholders(configuration.ThreadId, agent),
            UserId = ReplaceMemoryPlaceholders(configuration.UserId, agent)
        };

        if (string.IsNullOrWhiteSpace(options.ApplicationId)
            && string.IsNullOrWhiteSpace(options.AgentId)
            && string.IsNullOrWhiteSpace(options.ThreadId)
            && string.IsNullOrWhiteSpace(options.UserId))
        {
            options.AgentId = agent.Id.ToString("N");
        }

        options.ContextPrompt = configuration.ContextPrompt;
        return options;
    }

    private static string? ReplaceMemoryPlaceholders(string? value, AgentDefinition agent)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("{agentId}", agent.Id.ToString("N"), StringComparison.OrdinalIgnoreCase)
            .Replace("{agentName}", agent.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{templateKey}", agent.TemplateKey, StringComparison.OrdinalIgnoreCase);
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

internal sealed class WorkspaceMemoryContextProvider(
    IReadOnlyList<AgentMemoryRecord> memory,
    int maxItems) : MessageAIContextProvider
{
    private readonly IReadOnlyList<AgentMemoryRecord> memory = memory.ToList();
    private readonly int maxItems = Math.Clamp(maxItems, 1, 20);

    protected override ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        var selected = SelectRelevantMemory(context.RequestMessages)
            .Take(maxItems)
            .ToList();

        if (selected.Count == 0)
        {
            return new ValueTask<IEnumerable<ChatMessage>>([]);
        }

        var builder = new StringBuilder();
        builder.AppendLine("Workspace memory that may help with this task:");
        foreach (var item in selected)
        {
            builder.Append("- [")
                .Append(item.Kind)
                .Append("] ")
                .Append(item.Title)
                .Append(": ")
                .AppendLine(item.Content);
        }

        return new ValueTask<IEnumerable<ChatMessage>>(
        [
            new ChatMessage(ChatRole.System, builder.ToString().Trim())
        ]);
    }

    private IEnumerable<AgentMemoryRecord> SelectRelevantMemory(IEnumerable<ChatMessage> requestMessages)
    {
        var requestMessageSnapshot = requestMessages as IReadOnlyList<ChatMessage> ?? requestMessages.ToList();
        var requestText = string.Join(Environment.NewLine, requestMessageSnapshot.Select(message => message.Text));
        var terms = WorkspaceSearchSupport.TokenizeQuery(requestText);

        return memory
            .Select(item => new
            {
                Item = item,
                Score = ScoreMemory(item, terms)
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Item.Importance)
            .ThenByDescending(item => item.Item.CreatedAtUtc)
            .Select(item => item.Item);
    }

    private static int ScoreMemory(AgentMemoryRecord record, IReadOnlyList<string> terms)
    {
        var text = $"{record.Title}\n{record.Content}";
        var score = record.Importance * 4;
        foreach (var term in terms)
        {
            score += WorkspaceSearchSupport.CountOccurrences(text, term) * 5;
        }

        return score;
    }
}

internal sealed class StaticMessageContextProvider(ChatMessage message) : MessageAIContextProvider
{
    private readonly ChatMessage message = message;

    protected override ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        return new ValueTask<IEnumerable<ChatMessage>>([message]);
    }
}
