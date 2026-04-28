using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentChatModalTests
{
    [Fact]
    public async Task Agent_switch_dialog_returns_clicked_agent_and_keeps_cards_compact()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var first = CreateAgent("Primary Agent", "Implementation specialist");
        var second = CreateAgent("Review Agent", "QA review lead");
        var dialogService = context.Services.GetRequiredService<DialogService>();

        var resultTask = dialogService.OpenAsync<AgentSwitchDialog>(
            "Switch Agent",
            new Dictionary<string, object?>
            {
                [nameof(AgentSwitchDialog.Agents)] = new[] { first, second },
                [nameof(AgentSwitchDialog.SelectedAgentId)] = first.Id
            });

        host.WaitForElement("[data-testid='agent-switch-card']");
        var cards = host.FindAll("[data-testid='agent-switch-card']");

        Assert.Equal(2, cards.Count);
        Assert.Contains("Primary Agent", cards[0].TextContent);
        Assert.Contains("Current agent", cards[0].TextContent);
        Assert.Contains("QA review lead", cards[1].TextContent);

        cards[1].Click();

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(second.Id, Assert.IsType<Guid>(result));
    }

    [Fact]
    public void Agent_switch_dialog_filters_by_search_and_tags_and_sorts_favorites_first()
    {
        using var context = CreateContext();
        var implementation = CreateAgent("Alpha Developer", ".NET implementation specialist", "implementation");
        var review = CreateAgent("Zulu Review Lead", ".NET QA specialist", "quality", AgentSpecialTags.Favorite);
        var diagnostics = CreateAgent("Diagnostics Operator", "Runtime analyst", "diagnostics");

        var cut = context.RenderComponent<AgentSwitchDialog>(parameters => parameters
            .Add(component => component.Agents, new[] { implementation, diagnostics, review })
            .Add(component => component.SelectedAgentId, implementation.Id)
            .Add(component => component.FavoriteToggled, agent =>
            {
                var next = agent with
                {
                    Tags = agent.Tags
                        .Append(AgentSpecialTags.Favorite)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
                return Task.FromResult(next);
            }));

        var cards = cut.FindAll("[data-testid='agent-switch-card']");
        Assert.Equal(3, cards.Count);
        Assert.Contains("Zulu Review Lead", cards[0].TextContent);

        cut.Find("[data-testid='agent-switch-search']").Input("alpha");
        cards = cut.FindAll("[data-testid='agent-switch-card']");
        Assert.Single(cards);
        Assert.Contains("Alpha Developer", cards[0].TextContent);

        cut.Find("[data-testid='agent-switch-search']").Input(string.Empty);
        cut.Find("[data-testid='agent-switch-tag-filter-input']").Input("quality,");
        cards = cut.FindAll("[data-testid='agent-switch-card']");
        Assert.Single(cards);
        Assert.Contains("Zulu Review Lead", cards[0].TextContent);
    }

    [Fact]
    public void Agent_switch_dialog_toggles_favorite_and_promotes_agent()
    {
        using var context = CreateContext();
        var implementation = CreateAgent("Alpha Developer", ".NET implementation specialist", "implementation");
        var review = CreateAgent("Zulu Review Lead", ".NET QA specialist", AgentSpecialTags.Favorite);

        var cut = context.RenderComponent<AgentSwitchDialog>(parameters => parameters
            .Add(component => component.Agents, new[] { implementation, review })
            .Add(component => component.FavoriteToggled, agent =>
            {
                var next = agent with
                {
                    Tags = agent.Tags
                        .Append(AgentSpecialTags.Favorite)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
                return Task.FromResult(next);
            }));

        Assert.Contains("Zulu Review Lead", cut.FindAll("[data-testid='agent-switch-card']")[0].TextContent);

        cut.FindAll("[data-testid='agent-favorite-toggle']")[1].Click();

        var cards = cut.FindAll("[data-testid='agent-switch-card']");
        Assert.Contains("Alpha Developer", cards[0].TextContent);
    }

    [Fact]
    public void Runtime_details_dialog_projects_the_hidden_right_rail_content()
    {
        using var context = CreateContext();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var startedAtUtc = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero);

        var cut = context.RenderComponent<AgentRuntimeDetailsDialog>(parameters => parameters
            .Add(component => component.Run, CreateRun(agentId, sessionId, runId, startedAtUtc))
            .Add(component => component.ExecutionLog, new[]
            {
                CreateEntry(agentId, sessionId, runId, startedAtUtc.AddSeconds(1), ExecutionState.Preparing, "Preparing", "Opening the runtime session."),
                CreateEntry(agentId, sessionId, runId, startedAtUtc.AddSeconds(2), ExecutionState.Running, "Tool call", "Calling the workspace tool.")
            })
            .Add(component => component.Metrics, new[]
            {
                CreateMetric(agentId, sessionId, runId, startedAtUtc.AddSeconds(3))
            })
            .Add(component => component.RunStateText, "Running")
            .Add(component => component.RunStateTone, "info"));

        Assert.Contains("Selected execution", cut.Markup);
        Assert.Contains("Provider TestProvider / test-model", cut.Markup);
        Assert.Contains("Live execution timeline", cut.Markup);
        Assert.Contains("Tool call", cut.Markup);
        Assert.Contains("Metrics", cut.Markup);
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static AgentDefinition CreateAgent(string name, string role, params string[] tags)
    {
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: name,
            RoleTitle: role,
            Summary: $"{name} handles scoped technical work with durable evidence.",
            Instructions: "Keep work scoped and explicit.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: true,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: tags,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static ExecutionRunRecord CreateRun(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        DateTimeOffset startedAtUtc)
    {
        return new ExecutionRunRecord(
            Id: runId,
            AgentId: agentId,
            ChatSessionId: sessionId,
            Title: "Runtime thread",
            SourceKind: "chat",
            SourceId: sessionId.ToString(),
            CorrelationId: runId.ToString(),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Test prompt",
            ResultSummary: string.Empty,
            ProviderName: "TestProvider",
            Model: "test-model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: startedAtUtc,
            UpdatedAtUtc: startedAtUtc,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static ExecutionLogEntry CreateEntry(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        DateTimeOffset createdAtUtc,
        ExecutionState state,
        string phase,
        string message)
    {
        return new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: sessionId,
            CreatedAtUtc: createdAtUtc,
            State: state,
            Phase: phase,
            Message: message)
        {
            ExecutionRunId = runId
        };
    }

    private static AgentRunMetric CreateMetric(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        DateTimeOffset createdAtUtc)
    {
        return new AgentRunMetric(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: sessionId,
            CreatedAtUtc: createdAtUtc,
            Outcome: RunOutcome.Succeeded,
            ProviderName: "TestProvider",
            Model: "test-model",
            DurationMs: 1200,
            InputTokens: 14,
            OutputTokens: 21,
            ToolCalls: 2)
        {
            ExecutionRunId = runId
        };
    }
}
