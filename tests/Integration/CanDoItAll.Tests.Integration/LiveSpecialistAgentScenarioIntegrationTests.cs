using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class LiveSpecialistAgentScenarioIntegrationTests
{
    private const string RepositoryRoot = @"C:\repositories\CanDoItAll";
    private const string LiveValidationVariable = "CANDOITALL_RUN_LIVE_AGENT_VALIDATION";
    private const string LiveOpenAiSmokeVariable = "CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE";

    [Fact]
    [Trait("Category", "LiveAgent")]
    public async Task Business_finance_and_marketing_specialists_return_expected_handoff_artifacts()
    {
        if (!IsLiveValidationEnabled())
        {
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));

        var availability = await PostgresTestAvailability.EnsureAvailableAsync(RepositoryRoot);
        Assert.True(availability.IsAvailable, availability.Message);
        Assert.False(string.IsNullOrWhiteSpace(availability.ConnectionString));

        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-live-specialist-agents");
        var databaseName = $"cditall_live_{Guid.NewGuid():N}"[..30];
        await CreateDatabaseAsync(availability.ConnectionString!, databaseName);

        try
        {
            var profile = testEnvironment.CreatePostgreSqlProfile(
                "live-specialist-agent-postgres",
                BuildDatabaseConnectionString(availability.ConnectionString!, databaseName));
            await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
            {
                TestEnvironment = testEnvironment,
                ActiveProfile = profile
            });
            await using var scope = application.Services.CreateAsyncScope();
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
            var businessStrategist = FindAgent(agents, "Business Strategist");
            var financialStrategist = FindAgent(agents, "Financial Strategist");
            var marketingSpecialist = FindAgent(agents, "Marketing Specialist");

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var strategyBrief = await SendAndAssertAsync(
                workspaceService,
                businessStrategist.Id,
                """
                Do not call tools. Return a concise plain-text artifact for this scenario.
                Scenario: A two-person team wants to launch a paid AI inbox triage service for independent accountants.

                Include these exact labels:
                ARTIFACT: STRATEGY_BRIEF
                FOLDER:
                ASSUMPTIONS:
                FINANCE_HANDOFF:
                MARKETING_HANDOFF:
                """,
                ["ARTIFACT: STRATEGY_BRIEF", "FINANCE_HANDOFF:", "MARKETING_HANDOFF:"],
                timeout.Token);

            var financialModel = await SendAndAssertAsync(
                workspaceService,
                financialStrategist.Id,
                $"""
                Do not call tools. Convert this strategy brief into a handoff artifact for finance review.

                Strategy brief:
                {strategyBrief}

                Include these exact labels:
                ARTIFACT: FINANCIAL_MODEL
                DRIVERS:
                SENSITIVITY:
                DATA_GAPS:
                MARKETING_HANDOFF:
                """,
                ["ARTIFACT: FINANCIAL_MODEL", "DRIVERS:", "SENSITIVITY:", "MARKETING_HANDOFF:"],
                timeout.Token);

            await SendAndAssertAsync(
                workspaceService,
                marketingSpecialist.Id,
                $"""
                Do not call tools. Convert these upstream artifacts into a go-to-market handoff.

                Strategy brief:
                {strategyBrief}

                Finance artifact:
                {financialModel}

                Include these exact labels:
                ARTIFACT: GO_TO_MARKET_PLAN
                AUDIENCE:
                POSITIONING:
                CHANNELS:
                EXPERIMENTS:
                """,
                ["ARTIFACT: GO_TO_MARKET_PLAN", "AUDIENCE:", "POSITIONING:", "EXPERIMENTS:"],
                timeout.Token);
        }
        finally
        {
            await DropDatabaseAsync(availability.ConnectionString!, databaseName);
        }
    }

    private static bool IsLiveValidationEnabled()
    {
        return IsEnabled(LiveValidationVariable) &&
            IsEnabled(LiveOpenAiSmokeVariable);
    }

    private static bool IsEnabled(string environmentVariable)
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(environmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static AgentDefinition FindAgent(IReadOnlyList<AgentDefinition> agents, string name)
    {
        return Assert.Single(agents, agent => string.Equals(agent.Name, name, StringComparison.Ordinal));
    }

    private static async Task<string> SendAndAssertAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid agentId,
        string prompt,
        IReadOnlyList<string> requiredMarkers,
        CancellationToken cancellationToken)
    {
        var result = await workspaceService.SendMessageAsync(
            agentId,
            null,
            prompt,
            new AgentChatRunOptions(AgentExecutionOperationId.New()),
            cancellationToken);
        var content = result.AssistantMessage.Content;

        foreach (var marker in requiredMarkers)
        {
            Assert.True(
                content.Contains(marker, StringComparison.OrdinalIgnoreCase),
                $"Expected live agent response to contain marker '{marker}'. Response: {content}");
        }

        return content;
    }

    private static string BuildDatabaseConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            IncludeErrorDetail = true,
            Timeout = 5,
            CommandTimeout = 15
        };

        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildAdminConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"create database \"{databaseName}\";";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildAdminConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"drop database if exists \"{databaseName}\" with (force);";
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildAdminConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            builder.Database = "postgres";
        }

        builder.IncludeErrorDetail = true;
        builder.Timeout = 5;
        builder.CommandTimeout = 15;
        return builder.ConnectionString;
    }
}
