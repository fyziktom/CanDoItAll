using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration;

internal sealed class ScriptedProjectStructureMafProviderAgentFactory(
    ScriptedProjectStructureChatClient chatClient) : IMafProviderAgentFactory
{
    public AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses)
    {
        chatClient.RecordFactoryInvocation(provider, model);
        return chatClient.AsAIAgent(options: options);
    }
}

internal sealed class ScriptedReadOnlyProjectStructureMafProviderAgentFactory(
    ScriptedReadOnlyProjectStructureChatClient chatClient) : IMafProviderAgentFactory
{
    public AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses)
    {
        chatClient.RecordFactoryInvocation(provider, model);
        return chatClient.AsAIAgent(options: options);
    }
}

internal sealed class ScriptedProjectStructureChatClient : IChatClient
{
    public const string ProjectLeaseAcquireToolName = "project_structure_project_lease_acquire";
    public const string NodeCreateToolName = "project_structure_node_create";
    public const string NodesCopyToolName = "project_structure_nodes_copy";
    public const string StructureReadToolName = "project_structure_read";
    public const string LeaseReleaseToolName = "project_structure_lease_release";

    private const string LeaseCallId = "project-lease-acquire";
    private const string NodeCreateCallId = "project-node-create";
    private const string NodesCopyCallId = "project-nodes-copy";
    private const string StructureReadCallId = "project-structure-read";
    private const string LeaseReleaseCallId = "project-lease-release";
    private const string CompletionText = "Deterministic Project Structure tool loop completed.";

    private static readonly JsonSerializerOptions FunctionResultJsonOptions = CreateFunctionResultJsonOptions();
    private readonly Lock sync = new();
    private readonly List<IReadOnlySet<string>> capturedToolNames = [];
    private readonly List<string> issuedToolNames = [];
    private Scenario? scenario;
    private int responseIndex;

    public int InvocationCount { get; private set; }

    public string FactoryProviderName { get; private set; } = string.Empty;

    public string FactoryModel { get; private set; } = string.Empty;

    public ProjectStructureLeaseSnapshot? AcquiredLease { get; private set; }

    public ProjectStructureNodeSummary? CreatedNode { get; private set; }

    public ProjectStructureNodesCopyResult? CopyResult { get; private set; }

    public ProjectStructureReadToolData? CanonicalReadback { get; private set; }

    public ProjectStructureLeaseSnapshot? ReleasedLease { get; private set; }

    public IReadOnlyList<IReadOnlySet<string>> CapturedToolNames
    {
        get
        {
            lock (sync)
            {
                return capturedToolNames.ToArray();
            }
        }
    }

    public IReadOnlyList<string> IssuedToolNames
    {
        get
        {
            lock (sync)
            {
                return issuedToolNames.ToArray();
            }
        }
    }

    public void ConfigureScenario(Guid projectId, string parentNodeId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(parentNodeId);
        lock (sync)
        {
            if (scenario is not null)
            {
                throw new InvalidOperationException("The scripted Project Structure scenario is already configured.");
            }

            scenario = new NodeCreateScenario(projectId, parentNodeId);
        }
    }

    public void ConfigureCopyScenario(
        Guid projectId,
        IReadOnlyList<string> sourceNodeIds,
        string destinationParentNodeId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(sourceNodeIds);
        if (sourceNodeIds.Count == 0 || sourceNodeIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-blank source node id is required.", nameof(sourceNodeIds));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destinationParentNodeId);
        lock (sync)
        {
            if (scenario is not null)
            {
                throw new InvalidOperationException("The scripted Project Structure scenario is already configured.");
            }

            scenario = new NodesCopyScenario(
                projectId,
                sourceNodeIds.ToArray(),
                destinationParentNodeId);
        }
    }

    public void RecordFactoryInvocation(ProviderProfile provider, string model)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        lock (sync)
        {
            FactoryProviderName = provider.Name;
            FactoryModel = model;
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var messageSnapshot = messages.ToArray();

        lock (sync)
        {
            var configuredScenario = scenario
                ?? throw new InvalidOperationException("The scripted Project Structure scenario was not configured.");
            CaptureTools(options, configuredScenario);
            InvocationCount++;

            // Auto-approved non-interactive runs use the production implicit
            // mutation-lease protocol: mutation tools acquire and release their
            // own lease, and explicit lease tools are deliberately not exposed
            // for this purpose (explicit lease choreography belongs to governed
            // process automation).
            return Task.FromResult(responseIndex++ switch
            {
                0 => CreateMutationCallResponse(configuredScenario),
                1 => CreateReadCallResponse(messageSnapshot, configuredScenario),
                2 => CreateCompletionResponse(messageSnapshot),
                _ => throw new InvalidOperationException("The scripted Project Structure scenario received an unexpected provider turn.")
            });
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType.IsInstanceOfType(this)
            ? this
            : null;
    }

    public void Dispose()
    {
    }

    private ChatResponse CreateMutationCallResponse(Scenario configuredScenario)
    {
        return configuredScenario switch
        {
            NodeCreateScenario nodeCreate => CreateNodeCallResponse(nodeCreate),
            NodesCopyScenario nodesCopy => CreateNodesCopyCallResponse(nodesCopy),
            _ => throw new InvalidOperationException("The scripted Project Structure scenario type is unsupported.")
        };
    }

    private ChatResponse CreateNodeCallResponse(NodeCreateScenario configuredScenario)
        => CreateFunctionCallResponse(
            NodeCreateCallId,
            NodeCreateToolName,
            new Dictionary<string, object?>
            {
                ["projectId"] = configuredScenario.ProjectId,
                ["request"] = new ProjectStructureNodeCreateInput(
                    ProjectObjectType.ProjectBlock,
                    "Deterministic MAF child",
                    "Created through a scripted provider and real MAF tools",
                    "Canonical persistence proof for the Project Structure prompt harness.",
                    configuredScenario.ParentNodeId,
                    ObjectSubtype: "architecture",
                    LeaseToken: null)
            });

    private ChatResponse CreateNodesCopyCallResponse(NodesCopyScenario configuredScenario)
        => CreateFunctionCallResponse(
            NodesCopyCallId,
            NodesCopyToolName,
            new Dictionary<string, object?>
            {
                ["projectId"] = configuredScenario.ProjectId,
                ["request"] = new ProjectStructureNodesCopyInput(
                    configuredScenario.SourceNodeIds,
                    configuredScenario.DestinationParentNodeId,
                    LeaseToken: null)
            });

    private ChatResponse CreateReadCallResponse(
        IReadOnlyList<ChatMessage> messages,
        Scenario configuredScenario)
    {
        var readNodeIds = configuredScenario switch
        {
            NodeCreateScenario => ReadCreatedNodeId(messages),
            NodesCopyScenario => ReadCopiedNodeIds(messages),
            _ => throw new InvalidOperationException("The scripted Project Structure scenario type is unsupported.")
        };
        return CreateFunctionCallResponse(
            StructureReadCallId,
            StructureReadToolName,
            new Dictionary<string, object?>
            {
                ["projectId"] = configuredScenario.ProjectId,
                ["request"] = new ProjectStructureReadRequest(
                    NodeIds: readNodeIds,
                    IncludeLinks: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Source: ProjectStructureReadSource.CanonicalCurrent)
            });
    }

    private IReadOnlyList<string> ReadCreatedNodeId(IReadOnlyList<ChatMessage> messages)
    {
        CreatedNode = ReadFunctionResult<ProjectStructureNodeSummary>(messages, NodeCreateCallId);
        return [CreatedNode.Id];
    }

    private IReadOnlyList<string> ReadCopiedNodeIds(IReadOnlyList<ChatMessage> messages)
    {
        CopyResult = ReadFunctionResult<ProjectStructureNodesCopyResult>(messages, NodesCopyCallId);
        return CopyResult.NodeMappings
            .Select(mapping => mapping.CopiedNodeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private ChatResponse CreateCompletionResponse(IReadOnlyList<ChatMessage> messages)
    {
        CanonicalReadback = ReadFunctionResult<ProjectStructureReadToolData>(messages, StructureReadCallId);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, CompletionText));
    }

    private ChatResponse CreateFunctionCallResponse(
        string callId,
        string toolName,
        IDictionary<string, object?> arguments)
    {
        issuedToolNames.Add(toolName);
        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent(callId, toolName, arguments)]));
    }

    private void CaptureTools(ChatOptions? options, Scenario configuredScenario)
    {
        var toolNames = options?.Tools?
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
        capturedToolNames.Add(toolNames);

        var mutationToolName = configuredScenario switch
        {
            NodeCreateScenario => NodeCreateToolName,
            NodesCopyScenario => NodesCopyToolName,
            _ => throw new InvalidOperationException("The scripted Project Structure scenario type is unsupported.")
        };
        string[] missingTools =
        [
            mutationToolName,
            StructureReadToolName
        ];
        missingTools = missingTools
            .Where(toolName => !toolNames.Contains(toolName))
            .ToArray();
        if (missingTools.Length > 0)
        {
            throw new InvalidOperationException(
                $"Real MAF did not attach the required Project Structure tools: {string.Join(", ", missingTools)}.");
        }

        // The auto-approved non-interactive purpose must NOT expose explicit
        // lease tools; the implicit mutation lease is the production contract.
        string[] forbiddenLeaseTools =
        [
            ProjectLeaseAcquireToolName,
            LeaseReleaseToolName
        ];
        var unexpectedLeaseTools = forbiddenLeaseTools
            .Where(toolNames.Contains)
            .ToArray();
        if (unexpectedLeaseTools.Length > 0)
        {
            throw new InvalidOperationException(
                $"Real MAF attached explicit lease tools to an auto-approved non-interactive run: {string.Join(", ", unexpectedLeaseTools)}.");
        }
    }

    internal static T ReadFunctionResult<T>(
        IReadOnlyList<ChatMessage> messages,
        string callId)
    {
        var result = messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault(content => string.Equals(content.CallId, callId, StringComparison.Ordinal))
            ?.Result
            ?? throw new InvalidOperationException($"Function result '{callId}' was not supplied to the scripted provider.");

        if (result is T typedResult)
        {
            return typedResult;
        }

        var json = result is string text
            ? text
            : JsonSerializer.Serialize(result, FunctionResultJsonOptions);
        return JsonSerializer.Deserialize<T>(json, FunctionResultJsonOptions)
            ?? throw new InvalidOperationException($"Function result '{callId}' could not be read as {typeof(T).Name}.");
    }

    private static JsonSerializerOptions CreateFunctionResultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private abstract record Scenario(Guid ProjectId);

    private sealed record NodeCreateScenario(Guid ProjectId, string ParentNodeId) : Scenario(ProjectId);

    private sealed record NodesCopyScenario(
        Guid ProjectId,
        IReadOnlyList<string> SourceNodeIds,
        string DestinationParentNodeId) : Scenario(ProjectId);
}

internal sealed class ScriptedReadOnlyProjectStructureChatClient : IChatClient
{
    public const string StructureReadToolName = "project_structure_read";
    public const string CompletionText = "NOT ATTACHED: project-structure write authority unavailable.";

    private const string StructureReadCallId = "read-only-project-structure-read";

    private readonly Lock sync = new();
    private readonly List<IReadOnlySet<string>> capturedToolNames = [];
    private readonly List<string> issuedToolNames = [];
    private Guid? projectId;
    private int responseIndex;

    public int InvocationCount { get; private set; }

    public string FactoryProviderName { get; private set; } = string.Empty;

    public string FactoryModel { get; private set; } = string.Empty;

    public ProjectStructureReadToolData? CanonicalReadback { get; private set; }

    public IReadOnlyList<IReadOnlySet<string>> CapturedToolNames
    {
        get
        {
            lock (sync)
            {
                return capturedToolNames.ToArray();
            }
        }
    }

    public IReadOnlyList<string> IssuedToolNames
    {
        get
        {
            lock (sync)
            {
                return issuedToolNames.ToArray();
            }
        }
    }

    public void ConfigureScenario(Guid configuredProjectId)
    {
        if (configuredProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(configuredProjectId));
        }

        lock (sync)
        {
            if (projectId.HasValue)
            {
                throw new InvalidOperationException("The scripted read-only Project Structure scenario is already configured.");
            }

            projectId = configuredProjectId;
        }
    }

    public void RecordFactoryInvocation(ProviderProfile provider, string model)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        lock (sync)
        {
            FactoryProviderName = provider.Name;
            FactoryModel = model;
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var messageSnapshot = messages.ToArray();

        lock (sync)
        {
            var configuredProjectId = projectId
                ?? throw new InvalidOperationException("The scripted read-only Project Structure scenario was not configured.");
            CaptureTools(options);
            InvocationCount++;

            return Task.FromResult(responseIndex++ switch
            {
                0 => CreateReadCallResponse(configuredProjectId),
                1 => CreateCompletionResponse(messageSnapshot),
                _ => throw new InvalidOperationException("The scripted read-only Project Structure scenario received an unexpected provider turn.")
            });
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType.IsInstanceOfType(this)
            ? this
            : null;
    }

    public void Dispose()
    {
    }

    private ChatResponse CreateReadCallResponse(Guid configuredProjectId)
    {
        issuedToolNames.Add(StructureReadToolName);
        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        StructureReadCallId,
                        StructureReadToolName,
                        new Dictionary<string, object?>
                        {
                            ["projectId"] = configuredProjectId,
                            ["request"] = new ProjectStructureReadRequest(
                                IncludeLinks: true,
                                IncludeLayout: true,
                                IncludeMetadata: true,
                                IncludeNotes: true,
                                IncludeAssets: true,
                                Source: ProjectStructureReadSource.CanonicalCurrent)
                        })
                ]));
    }

    private ChatResponse CreateCompletionResponse(IReadOnlyList<ChatMessage> messages)
    {
        CanonicalReadback = ScriptedProjectStructureChatClient.ReadFunctionResult<ProjectStructureReadToolData>(
            messages,
            StructureReadCallId);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, CompletionText));
    }

    private void CaptureTools(ChatOptions? options)
    {
        var toolNames = options?.Tools?
            .Select(tool => tool.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
        capturedToolNames.Add(toolNames);

        if (!toolNames.Contains(StructureReadToolName))
        {
            throw new InvalidOperationException(
                $"Real MAF did not attach the required Project Structure read tool '{StructureReadToolName}'.");
        }
    }
}
