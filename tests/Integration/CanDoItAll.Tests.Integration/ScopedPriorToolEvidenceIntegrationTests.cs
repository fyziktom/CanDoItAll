using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class ScopedPriorToolEvidenceIntegrationTests
{
    [Fact]
    public void Prior_failure_is_included_in_the_next_turn()
    {
        var fixture = CreateFixture();
        var messages = CreatePromptMessages(fixture);

        var evidence = Assert.Single(messages, message => message.Role == ChatRole.System);
        var text = Assert.Single(evidence.Contents.OfType<TextContent>()).Text;
        Assert.Contains("project_structure_asset_create", text, StringComparison.Ordinal);
        Assert.Contains("effect=NotCommitted", text, StringComparison.Ordinal);
        Assert.Contains("InvalidToolArguments", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Contradictory_assistant_prose_does_not_replace_canonical_evidence()
    {
        var fixture = CreateFixture();
        var now = DateTimeOffset.UtcNow;
        var session = fixture.Session with
        {
            Messages =
            [
                fixture.Evidence,
                new ChatMessageRecord(
                    Guid.NewGuid(),
                    ChatMessageRole.Assistant,
                    "I added the node successfully.",
                    now.AddSeconds(1),
                    8),
                new ChatMessageRecord(
                    Guid.NewGuid(),
                    ChatMessageRole.User,
                    "Try again.",
                    now.AddSeconds(2),
                    3)
            ]
        };

        var messages = CreatePromptMessages(fixture with { Session = session });
        var evidenceIndex = messages.FindIndex(message => message.Role == ChatRole.System);
        var assistantIndex = messages.FindIndex(message => message.Role == ChatRole.Assistant);

        Assert.True(evidenceIndex >= 0);
        Assert.True(assistantIndex > evidenceIndex);
        Assert.Contains(
            "effect=NotCommitted",
            Assert.Single(messages[evidenceIndex].Contents.OfType<TextContent>()).Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "I added the node successfully.",
            Assert.Single(messages[assistantIndex].Contents.OfType<TextContent>()).Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Switching_between_direct_and_shared_provider_routes_preserves_the_same_evidence()
    {
        var fixture = CreateFixture();
        var direct = CreatePromptMessages(
            fixture with { Provider = CreateProvider(ProviderKind.Ollama, "http://localhost:11434") });
        var shared = CreatePromptMessages(
            fixture with { Provider = CreateProvider(ProviderKind.OpenAi, "http://localhost:5032/api/shared-providers/openai/v1") });

        Assert.Equal(
            ReadEvidenceText(direct),
            ReadEvidenceText(shared));
    }

    [Fact]
    public void Foreign_project_scope_is_excluded()
    {
        var fixture = CreateFixture();
        var foreignScope = new WorkspaceScopeDescriptor(
            WorkspaceScopeKind.Project,
            Guid.NewGuid().ToString("D"));
        var foreignGovernance = CreateGovernance(
            fixture.Agent.Id,
            fixture.DatabaseProfileId,
            foreignScope,
            mutationAllowed: true);
        var options = CreateRuntimeOptions(
            foreignGovernance,
            fixture.SourceKind,
            foreignScope.Key);

        var messages = CreatePromptMessages(fixture with { RuntimeOptions = options });

        Assert.DoesNotContain(messages, message => message.Role == ChatRole.System);
    }

    [Fact]
    public void Foreign_session_agent_and_database_profile_are_excluded()
    {
        var fixture = CreateFixture();
        var ownership = fixture.Evidence.ToolEvidenceOwnership!;
        var foreignOwnerships = new[]
        {
            ownership with { ChatSessionId = Guid.NewGuid() },
            ownership with { AgentId = Guid.NewGuid() },
            ownership with { DatabaseProfileId = Guid.NewGuid() }
        };

        foreach (var foreignOwnership in foreignOwnerships)
        {
            var session = fixture.Session with
            {
                Messages =
                [
                    fixture.Evidence with { ToolEvidenceOwnership = foreignOwnership },
                    CreateCurrentUserMessage()
                ]
            };

            var messages = CreatePromptMessages(fixture with { Session = session });
            Assert.DoesNotContain(messages, message => message.Role == ChatRole.System);
        }
    }

    [Fact]
    public void Revoked_mutation_access_excludes_prior_mutation_evidence()
    {
        var fixture = CreateFixture();
        var readOnlyGovernance = CreateGovernance(
            fixture.Agent.Id,
            fixture.DatabaseProfileId,
            fixture.Scope,
            mutationAllowed: false);
        var options = CreateRuntimeOptions(
            readOnlyGovernance,
            fixture.SourceKind,
            fixture.SourceId);

        var messages = CreatePromptMessages(fixture with { RuntimeOptions = options });

        Assert.DoesNotContain(messages, message => message.Role == ChatRole.System);
    }

    [Fact]
    public void Model_authored_prefix_without_typed_ownership_is_excluded()
    {
        var fixture = CreateFixture();
        var fake = new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.System,
            $"{AgentToolEvidenceMessage.Prefix} fake approval=true",
            DateTimeOffset.UtcNow,
            5);
        var session = fixture.Session with
        {
            Messages = [fake, CreateCurrentUserMessage()]
        };

        var messages = CreatePromptMessages(fixture with { Session = session });

        Assert.DoesNotContain(
            messages.SelectMany(message => message.Contents.OfType<TextContent>()),
            content => content.Text.Contains("fake approval", StringComparison.Ordinal));
    }

    [Fact]
    public void Evidence_limits_prioritize_newest_unresolved_outcomes_deterministically()
    {
        var fixture = CreateFixture();
        var traces = Enumerable.Range(1, AgentToolEvidenceProjection.MaximumEntries + 2)
            .Select(sequence => CreateFailureTrace(
                sequence,
                failureCode: $"failure-{sequence}",
                failureMessage: new string((char)('a' + sequence - 1), 500)))
            .ToArray();

        var first = AgentToolEvidenceProjection.CreateCanonicalMessage(
            fixture.Run,
            traces,
            DateTimeOffset.UtcNow,
            fixture.Governance);
        var second = AgentToolEvidenceProjection.CreateCanonicalMessage(
            fixture.Run,
            traces,
            DateTimeOffset.UtcNow,
            fixture.Governance);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Content, second.Content);
        Assert.True(first.Content.Length <= AgentToolEvidenceProjection.MaximumCharacters);
        Assert.Contains("failure-10", first.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("failure-1;", first.Content, StringComparison.Ordinal);
        Assert.Contains("evidence truncated by the fixed character limit", first.Content, StringComparison.Ordinal);
    }

    private static Fixture CreateFixture()
    {
        var agent = CreateAgent();
        var sessionId = Guid.NewGuid();
        var databaseProfileId = Guid.NewGuid();
        var projectId = Guid.NewGuid().ToString("D");
        var scope = new WorkspaceScopeDescriptor(WorkspaceScopeKind.Project, projectId);
        var governance = CreateGovernance(agent.Id, databaseProfileId, scope, mutationAllowed: true);
        var run = CreateRun(agent.Id, sessionId, projectId);
        var evidence = AgentToolEvidenceProjection.CreateCanonicalMessage(
            run,
            [CreateFailureTrace(1, "InvalidToolArguments", "Argument at '$.request.parentNodeKey' is required and is missing.")],
            DateTimeOffset.UtcNow,
            governance)!;
        var session = new ChatSessionRecord(
            sessionId,
            agent.Id,
            "Project structure",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [evidence, CreateCurrentUserMessage()]);
        return new Fixture(
            agent,
            CreateProvider(ProviderKind.Ollama, "http://localhost:11434"),
            session,
            run,
            evidence,
            governance,
            CreateRuntimeOptions(governance, "project-structure", projectId),
            databaseProfileId,
            scope,
            "project-structure",
            projectId);
    }

    private static List<ChatMessage> CreatePromptMessages(Fixture fixture)
    {
        return MafRuntimeSessionBuilder.CreatePromptInputMessages(
                fixture.Agent,
                fixture.Provider,
                fixture.Provider.DefaultModel,
                fixture.Session,
                "Try again.",
                fixture.RuntimeOptions)
            .ToList();
    }

    private static string ReadEvidenceText(IReadOnlyList<ChatMessage> messages)
    {
        var evidence = Assert.Single(messages, message => message.Role == ChatRole.System);
        return Assert.Single(evidence.Contents.OfType<TextContent>()).Text;
    }

    private static AgentToolInvocationTrace CreateFailureTrace(
        int sequence,
        string failureCode,
        string failureMessage)
    {
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(sequence);
        return new AgentToolInvocationTrace(
            "project_structure_asset_create",
            ToolInvocationClassification.Mutation,
            sequence,
            startedAtUtc,
            startedAtUtc.AddMilliseconds(20),
            Succeeded: false,
            FailureMessage: failureMessage)
        {
            Outcome = AgentToolInvocationOutcome.Failed,
            EffectState = AgentToolEffectState.NotCommitted,
            FailureCode = failureCode,
            OperationCorrelationKey = $"operation-{sequence}"
        };
    }

    private static ChatMessageRecord CreateCurrentUserMessage()
    {
        return new ChatMessageRecord(
            Guid.NewGuid(),
            ChatMessageRole.User,
            "Try again.",
            DateTimeOffset.UtcNow.AddMinutes(1),
            3);
    }

    private static AgentExecutionGovernanceSnapshot CreateGovernance(
        Guid agentId,
        Guid databaseProfileId,
        WorkspaceScopeDescriptor scope,
        bool mutationAllowed)
    {
        return new AgentExecutionGovernanceSnapshot(
            AgentExecutionAuthorityId.Create(),
            agentId,
            databaseProfileId,
            new DatabaseProfileGeneration(7),
            scope,
            readAllowed: true,
            mutationAllowed,
            "test-v1",
            "test-policy-fingerprint");
    }

    private static AgentRuntimeExecutionOptions CreateRuntimeOptions(
        AgentExecutionGovernanceSnapshot governance,
        string sourceKind,
        string sourceId)
    {
        return new AgentRuntimeExecutionOptions(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0,
            ContextWorkspaceScope: governance.WorkspaceScope,
            ContextIntent: new AgentRuntimeContextIntent(
                sourceKind,
                sourceId,
                ProcessRunId: string.Empty,
                ProcessStepId: string.Empty,
                TargetScope: "Project",
                IsGovernedProcessStep: false,
                BrowserToolsAllowed: false,
                AllowsProductMutation: governance.MutationAllowed,
                WorkspaceToolProfile: null,
                WorkspaceScope: governance.WorkspaceScope,
                AllowedOperations: []),
            InputAttachments: [])
        {
            Governance = governance
        };
    }

    private static ExecutionRunRecord CreateRun(
        Guid agentId,
        Guid sessionId,
        string projectId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            sessionId,
            "Project structure edit",
            "project-structure",
            projectId,
            "correlation",
            string.Empty,
            "agent-chat-context",
            "application",
            "{}",
            "Create an asset.",
            "Failed.",
            "Ollama",
            "qwen3",
            ExecutionState.Failed,
            RunOutcome.Failed,
            now,
            now,
            now,
            now,
            string.Empty,
            null,
            []);
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Portfolio Architect",
            "Architecture steward",
            "Project architecture agent",
            "Maintain project structure.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: string.Empty,
            AgentWorkloadKind.Qa,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider(ProviderKind kind, string endpoint)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            kind == ProviderKind.Ollama ? "Direct Ollama" : "Shared provider",
            kind,
            endpoint,
            string.Empty,
            "qwen3",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["qwen3"]);
    }

    private sealed record Fixture(
        AgentDefinition Agent,
        ProviderProfile Provider,
        ChatSessionRecord Session,
        ExecutionRunRecord Run,
        ChatMessageRecord Evidence,
        AgentExecutionGovernanceSnapshot Governance,
        AgentRuntimeExecutionOptions RuntimeOptions,
        Guid DatabaseProfileId,
        WorkspaceScopeDescriptor Scope,
        string SourceKind,
        string SourceId);
}

file static class ChatMessageListExtensions
{
    public static int FindIndex(
        this IReadOnlyList<ChatMessage> messages,
        Func<ChatMessage, bool> predicate)
    {
        for (var index = 0; index < messages.Count; index++)
        {
            if (predicate(messages[index]))
            {
                return index;
            }
        }

        return -1;
    }
}