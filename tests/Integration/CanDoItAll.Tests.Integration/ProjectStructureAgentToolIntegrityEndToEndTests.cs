using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class ProjectStructureAgentToolIntegrityEndToEndTests
{
    internal const string RequestedAssetTitle = "Integrity architecture overview";
    internal const string UnrelatedAssetTitle = "Different committed asset";
    internal const string FollowUpPrompt = "Review the prior tool failure before answering.";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

    [Fact]
    public async Task Malformed_call_followed_by_future_prose_creates_no_node_and_persists_failed_status()
    {
        await using var fixture = await CreateFixtureAsync(IntegrityScenarioKind.MalformedThenClaim);
        var before = await ReadStructureAsync(fixture);
        var observation = await ExecuteAsync(fixture, "Create the requested architecture asset and report success.");
        var after = await ReadStructureAsync(fixture);
        var detail = await ReadRunAsync(fixture, observation.ExecutionRunId);
        var receipt = Assert.Single(
            detail.ToolReceipts,
            item => string.Equals(
                item.ToolName,
                AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
                StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.OK, observation.StatusCode);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(RunOutcome.Failed, detail.Run.Outcome);
        Assert.Equal(AgentToolInvocationOutcome.Failed, receipt.InvocationOutcome);
        Assert.Equal(AgentToolEffectState.NotCommitted, receipt.EffectState);
        Assert.Equal("InvalidToolArguments", receipt.FailureCode);
        Assert.True(receipt.CanRetryWithCorrectedInput);
        Assert.Equal(
            before.Nodes.Select(node => node.Id).Order(StringComparer.Ordinal),
            after.Nodes.Select(node => node.Id).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(after.Nodes, node => node.Title == RequestedAssetTitle);
    }

    [Fact]
    public async Task Corrected_same_operation_creates_exactly_one_visible_canonical_node()
    {
        await using var fixture = await CreateFixtureAsync(IntegrityScenarioKind.CorrectedSameOperation);
        var observation = await ExecuteAsync(
            fixture,
            "Create the requested architecture asset and correct any invalid tool arguments.");
        var detail = await ReadRunAsync(fixture, observation.ExecutionRunId);
        var structure = await ReadStructureAsync(fixture);
        var created = Assert.Single(structure.Nodes, node => node.Title == RequestedAssetTitle);
        var receipts = AssetReceipts(detail);

        Assert.Equal(HttpStatusCode.OK, observation.StatusCode);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Equal(RunOutcome.Succeeded, detail.Run.Outcome);
        Assert.Equal(fixture.PrimaryParentNodeId, created.ParentId);
        Assert.Collection(
            receipts,
            failed =>
            {
                Assert.Equal(AgentToolInvocationOutcome.Failed, failed.InvocationOutcome);
                Assert.Equal(AgentToolEffectState.NotCommitted, failed.EffectState);
            },
            succeeded =>
            {
                Assert.Equal(AgentToolInvocationOutcome.Succeeded, succeeded.InvocationOutcome);
                Assert.Equal(AgentToolEffectState.Committed, succeeded.EffectState);
            });
    }

    [Fact]
    public async Task Next_turn_receives_scoped_canonical_failure_evidence()
    {
        await using var fixture = await CreateFixtureAsync(IntegrityScenarioKind.NextTurnEvidence);
        var failed = await ExecuteContextualAsync(fixture, "Create the requested architecture asset and report success.");
        var firstDetail = await ReadRunAsync(fixture, failed.ExecutionRunId);
        Assert.Equal(ExecutionState.Failed, firstDetail.Run.State);
        var persistedEvidence = Assert.Single(
            Assert.IsType<ChatSessionRecord>(firstDetail.ChatSession).Messages,
            message => message.ToolEvidenceOwnership is not null);
        Assert.Contains("InvalidToolArguments", persistedEvidence.Content, StringComparison.Ordinal);

        var completed = await ExecuteContextualAsync(fixture, FollowUpPrompt);
        var secondDetail = await ReadRunAsync(fixture, completed.ExecutionRunId);
        var evidence = Assert.IsType<string>(fixture.ChatClient.ObservedCanonicalEvidence);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(firstDetail.Run.ChatSessionId, secondDetail.Run.ChatSessionId);
        Assert.Equal(ExecutionState.Completed, secondDetail.Run.State);
        Assert.Contains(
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            evidence,
            StringComparison.Ordinal);
        Assert.Contains("InvalidToolArguments", evidence, StringComparison.Ordinal);
        Assert.Contains("effect=NotCommitted", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("Everything succeeded", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unrelated_committed_target_does_not_resolve_the_failed_operation()
    {
        await using var fixture = await CreateFixtureAsync(IntegrityScenarioKind.UnrelatedSuccess);
        var observation = await ExecuteAsync(
            fixture,
            "Create the requested architecture asset; do not substitute a different target.");
        var detail = await ReadRunAsync(fixture, observation.ExecutionRunId);
        var structure = await ReadStructureAsync(fixture);
        var unrelated = Assert.Single(structure.Nodes, node => node.Title == UnrelatedAssetTitle);
        var receipts = AssetReceipts(detail);

        Assert.Equal(HttpStatusCode.OK, observation.StatusCode);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(RunOutcome.Failed, detail.Run.Outcome);
        Assert.Equal(fixture.SecondaryParentNodeId, unrelated.ParentId);
        Assert.DoesNotContain(structure.Nodes, node => node.Title == RequestedAssetTitle);
        Assert.Equal(2, receipts.Length);
        Assert.Equal(AgentToolInvocationOutcome.Failed, receipts[0].InvocationOutcome);
        Assert.Equal(AgentToolInvocationOutcome.Succeeded, receipts[1].InvocationOutcome);
        Assert.Equal(AgentToolEffectState.Committed, receipts[1].EffectState);
    }

    private static ToolExecutionReceiptRecord[] AssetReceipts(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(item => string.Equals(
                item.ToolName,
                AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
                StringComparison.Ordinal))
            .OrderBy(item => item.StartedAtUtc)
            .ToArray();
    }

    private static async Task<IntegrityFixture> CreateFixtureAsync(IntegrityScenarioKind scenario)
    {
        var environmentKey = $"project-structure-tool-integrity-{scenario}-{Guid.NewGuid():N}";
        var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            environmentKey,
            environment => environment.CreatePostgreSqlProfile("tool-integrity"),
            services =>
            {
                services.AddSingleton<IntegrityScenarioChatClient>();
                services.Replace(
                    ServiceDescriptor.Singleton<IMafProviderAgentFactory, IntegrityScenarioMafProviderAgentFactory>());
            },
            RequestTimeout);
        var serviceScope = host.App.Services.CreateAsyncScope();
        try
        {
            var services = serviceScope.ServiceProvider;
            var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>());
            var primaryParentNodeId = $"project:{projectId:D}";
            var secondaryParent = await services
                .GetRequiredService<ProjectWorkbenchService>()
                .CreateObjectAsync(
                    projectId,
                    new ProjectObjectCreateRequest(
                        ProjectObjectType.ProjectBlock,
                        "Unrelated target parent",
                        "Separate operation identity",
                        string.Empty,
                        primaryParentNodeId,
                        ObjectSubtype: "architecture"));
            var workspaceService = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var agentId = await CreateAgentAsync(workspaceService, projectId);
            var session = await workspaceService.GetOrCreateChatSessionAsync(agentId);
            var chatClient = host.App.Services.GetRequiredService<IntegrityScenarioChatClient>();
            chatClient.Configure(scenario, projectId, primaryParentNodeId, secondaryParent.Id);
            var contextLease = services
                .GetRequiredService<IAgentChatContextRegistry>()
                .ActivateScope(new AgentChatContextScope(
                    AgentChatContextScopeId.Create(),
                    ProjectStructureAgentChatContextBuilder.BuildSource(projectId),
                    "Project structure",
                    WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
                    [
                        new AgentChatContextAgentAccess(
                            agentId,
                            AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                            "This project")
                    ],
                    AgentChatContextScopeAccessMode.AllowListed,
                    AgentChatContextAccessState.Ready,
                    completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun));
            return new IntegrityFixture(
                host,
                contextLease,
                serviceScope,
                chatClient,
                projectId,
                agentId,
                session.Id,
                primaryParentNodeId,
                secondaryParent.Id);
        }
        catch
        {
            await serviceScope.DisposeAsync();
            await host.DisposeAsync();
            throw;
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = $"Tool integrity {Guid.NewGuid():N}",
            Description = "Isolated project for deterministic real-MAF tool integrity proof.",
            Objective = "Prove truthful mutation completion and correction.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreateAgentAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid projectId)
    {
        var provider = (await workspaceService.ListProvidersAsync())
            .First(item => item.IsEnabled && item.SupportsTools && item.Purpose == ProviderProfilePurpose.Chat);
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Project Structure Tool Integrity Agent";
        editor.RoleTitle = "Portfolio architect";
        editor.Summary = "Exercises project asset mutation integrity through real MAF composition.";
        editor.Instructions = "Use Project Structure tools and report only verified outcomes.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.ProviderProfileId = provider.Id;
        editor.Model = provider.DefaultModel;
        editor.ConfigurationJson = "{}";
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.Permissions = AgentPermissionsPolicy.Default with
        {
            AutoApproveExternalCallsByDefault = true
        };
        editor.ProjectStructureAccess = new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = [projectId]
        };
        return await workspaceService.SaveAgentAsync(editor);
    }

    private static async Task<RunObservation> ExecuteAsync(IntegrityFixture fixture, string prompt)
    {
        var metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            "{}",
            WorkspaceScopeDescriptor.Project(fixture.ProjectId.ToString("D")));
        using var response = await fixture.Host.Client.PostAsJsonAsync(
            $"/api/agents/{fixture.AgentId:D}/execution-runs",
            new
            {
                prompt,
                chatSessionId = fixture.ChatSessionId,
                context = new ExecutionInvocationContext(
                    SourceKind: ProjectStructureAgentChatContextBuilder.SourceKind,
                    SourceId: fixture.ProjectId.ToString("D"),
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    CausationId: fixture.ChatSessionId.ToString("N"),
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: metadataJson),
                autoApprovePendingToolCalls = true
            });
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<ExecutionRunResult>(body, ApiJsonOptions)
                ?? throw new InvalidOperationException("The execution endpoint returned no result.");
            return new RunObservation(response.StatusCode, result.ExecutionRunId);
        }

        var failure = JsonSerializer.Deserialize<ApiErrorResponse>(body, ApiJsonOptions)
            ?? throw new InvalidOperationException("The execution endpoint returned no failure contract.");
        return new RunObservation(response.StatusCode, Assert.IsType<Guid>(failure.ExecutionRunId));
    }

    private static async Task<RunObservation> ExecuteContextualAsync(
        IntegrityFixture fixture,
        string prompt)
    {
        var services = fixture.ServiceScope.ServiceProvider;
        var capture = await services
            .GetRequiredService<IAgentTurnContextCaptureService>()
            .CaptureAsync(new AgentTurnContextCaptureCommand(
                fixture.AgentId,
                fixture.ChatSessionId,
                prompt,
                AgentExecutionOperationId.New(),
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(),
                AgentChatExecutionBehavior.Default));
        try
        {
            var result = await services
                .GetRequiredService<IAgentFrameworkWorkspaceService>()
                .SendMessageAsync(
                    fixture.AgentId,
                    fixture.ChatSessionId,
                    capture.Invocation.Prompt,
                    options: capture.Invocation.Options);
            return new RunObservation(HttpStatusCode.OK, result.ExecutionRunId);
        }
        catch (AgentChatRunFailedException exception)
        {
            return new RunObservation(HttpStatusCode.InternalServerError, exception.ExecutionRunId);
        }
    }

    private static async Task<ExecutionRunDetail> ReadRunAsync(IntegrityFixture fixture, Guid executionRunId)
    {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IAgentFrameworkWorkspaceService>()
            .GetExecutionRunDetailAsync(executionRunId);
    }

    private static async Task<ProjectStructureSurface> ReadStructureAsync(IntegrityFixture fixture)
    {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProjectWorkbenchService>()
            .GetStructureAsync(fixture.ProjectId);
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record RunObservation(HttpStatusCode StatusCode, Guid ExecutionRunId);

    private sealed record IntegrityFixture(
        ProjectStructureAgentApiTestHost Host,
        IAgentChatContextScopeLease ContextLease,
        AsyncServiceScope ServiceScope,
        IntegrityScenarioChatClient ChatClient,
        Guid ProjectId,
        Guid AgentId,
        Guid ChatSessionId,
        string PrimaryParentNodeId,
        string SecondaryParentNodeId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            ContextLease.Dispose();
            await ServiceScope.DisposeAsync();
            await Host.DisposeAsync();
        }
    }
}

internal enum IntegrityScenarioKind
{
    MalformedThenClaim,
    CorrectedSameOperation,
    NextTurnEvidence,
    UnrelatedSuccess
}

internal sealed class IntegrityScenarioMafProviderAgentFactory(
    IntegrityScenarioChatClient chatClient) : IMafProviderAgentFactory
{
    public AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses)
    {
        return chatClient.AsAIAgent(options: options);
    }
}

internal sealed class IntegrityScenarioChatClient : IChatClient
{
    private const string MalformedCallId = "asset-create-malformed";
    private const string CorrectedCallId = "asset-create-corrected";
    private static readonly JsonSerializerOptions ArgumentSerializerOptions = CreateArgumentSerializerOptions();
    private readonly Lock sync = new();
    private Scenario? scenario;
    private int responseIndex;

    public string? ObservedCanonicalEvidence { get; private set; }

    public void Configure(
        IntegrityScenarioKind kind,
        Guid projectId,
        string primaryParentNodeId,
        string secondaryParentNodeId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(primaryParentNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondaryParentNodeId);
        lock (sync)
        {
            if (scenario is not null)
            {
                throw new InvalidOperationException("The integrity scenario is already configured.");
            }

            scenario = new Scenario(kind, projectId, primaryParentNodeId, secondaryParentNodeId);
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
            var configured = scenario
                ?? throw new InvalidOperationException("The integrity scenario was not configured.");
            EnsureAssetToolIsAvailable(options);

            if (ContainsUserText(messageSnapshot, ProjectStructureAgentToolIntegrityEndToEndTests.FollowUpPrompt))
            {
                ObservedCanonicalEvidence = string.Join(
                    Environment.NewLine,
                    messageSnapshot
                        .SelectMany(message => message.Contents.OfType<TextContent>())
                        .Select(content => content.Text)
                        .Where(text => text.StartsWith(
                            AgentToolEvidenceMessage.Prefix,
                            StringComparison.Ordinal)));
                return Task.FromResult(CreateCompletionResponse("Prior canonical failure reviewed."));
            }

            return Task.FromResult(responseIndex++ switch
            {
                0 => CreateMalformedCallResponse(configured),
                1 when configured.Kind is IntegrityScenarioKind.CorrectedSameOperation =>
                    CreateCorrectedCallResponse(
                        configured,
                        configured.PrimaryParentNodeId,
                        ProjectStructureAgentToolIntegrityEndToEndTests.RequestedAssetTitle),
                1 when configured.Kind is IntegrityScenarioKind.UnrelatedSuccess =>
                    CreateCorrectedCallResponse(
                        configured,
                        configured.SecondaryParentNodeId,
                        ProjectStructureAgentToolIntegrityEndToEndTests.UnrelatedAssetTitle),
                1 => CreateCompletionResponse("Everything succeeded; the requested asset is available."),
                2 => CreateCompletionResponse("Everything succeeded; the requested asset is available."),
                _ => throw new InvalidOperationException(
                    "The scripted integrity provider received an unexpected turn.")
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

    private static ChatResponse CreateMalformedCallResponse(Scenario configured)
    {
        return CreateFunctionCallResponse(
            MalformedCallId,
            new Dictionary<string, object?>
            {
                ["projectId"] = configured.ProjectId,
                ["objectType"] = ProjectObjectType.File,
                ["title"] = ProjectStructureAgentToolIntegrityEndToEndTests.RequestedAssetTitle,
                ["subtitle"] = "Architecture document",
                ["notes"] = "The malformed flat payload must never execute.",
                ["media"] = new ProjectObjectMediaPayload(
                    "integrity-architecture.md",
                    "text/markdown",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("# Integrity architecture"))),
                ["parentNodeKey"] = configured.PrimaryParentNodeId,
                ["objectSubtype"] = "markdown"
            });
    }

    private static ChatResponse CreateCorrectedCallResponse(
        Scenario configured,
        string parentNodeId,
        string title)
    {
        return CreateFunctionCallResponse(
            CorrectedCallId,
            new Dictionary<string, object?>
            {
                ["projectId"] = configured.ProjectId,
                ["request"] = JsonSerializer.SerializeToElement(
                    new ProjectStructureAgentAssetCreateInput(
                        ProjectObjectType.File,
                        title,
                        "Architecture document",
                        "Created only through the corrected nested tool request.",
                        new ProjectObjectMediaPayload(
                            $"{title.Replace(' ', '-').ToLowerInvariant()}.md",
                            "text/markdown",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes($"# {title}"))),
                        ParentNodeKey: parentNodeId,
                        ObjectSubtype: "markdown"),
                    ArgumentSerializerOptions)
            });
    }

    private static ChatResponse CreateFunctionCallResponse(
        string callId,
        IDictionary<string, object?> arguments)
    {
        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        callId,
                        AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
                        arguments)
                ]));
    }

    private static ChatResponse CreateCompletionResponse(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    private static bool ContainsUserText(IReadOnlyList<ChatMessage> messages, string expected)
    {
        return messages
            .Where(message => message.Role == ChatRole.User)
            .SelectMany(message => message.Contents.OfType<TextContent>())
            .Any(content => content.Text.Contains(expected, StringComparison.Ordinal));
    }

    private static JsonSerializerOptions CreateArgumentSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void EnsureAssetToolIsAvailable(ChatOptions? options)
    {
        if (options?.Tools?.Any(tool => string.Equals(
                tool.Name,
                AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
                StringComparison.Ordinal)) != true)
        {
            throw new InvalidOperationException(
                "Real MAF did not attach the project structure asset-create tool.");
        }
    }

    private sealed record Scenario(
        IntegrityScenarioKind Kind,
        Guid ProjectId,
        string PrimaryParentNodeId,
        string SecondaryParentNodeId);
}
