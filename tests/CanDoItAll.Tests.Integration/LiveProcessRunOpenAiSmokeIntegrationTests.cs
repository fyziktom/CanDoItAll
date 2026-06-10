using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class LiveProcessRunOpenAiSmokeIntegrationTests
{
    private const string RepositoryRoot = @"C:\repositories\CanDoItAll";
    private const string LiveProcessRunValidationVariable = "CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION";
    private const string LiveOpenAiSmokeVariable = "CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE";
    private const string LiveModelVariable = "CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL";
    private const string LiveTimeoutSecondsVariable = "CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS";
    private const string LiveMaxTotalTokensVariable = "CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS";
    private const int DefaultTimeoutSeconds = 180;
    private const int DefaultMaxTotalTokens = 250_000;

    [Fact]
    [Trait("Category", "LiveProcessRun")]
    public async Task Process_run_dispatch_executes_bound_openai_agent_and_records_process_usage()
    {
        if (!IsLiveValidationEnabled())
        {
            return;
        }

        Assert.False(
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            "OPENAI_API_KEY must be set for live OpenAI process-run smoke validation; the value is never logged.");

        var model = ResolveTextSetting(LiveModelVariable, ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        var timeoutSeconds = ResolveBoundedIntegerSetting(LiveTimeoutSecondsVariable, DefaultTimeoutSeconds, 30, 300);
        var maxTotalTokens = ResolveBoundedIntegerSetting(LiveMaxTotalTokensVariable, DefaultMaxTotalTokens, 10_000, 500_000);
        var availability = await PostgresTestAvailability.EnsureAvailableAsync(RepositoryRoot);
        Assert.True(availability.IsAvailable, availability.Message);
        Assert.False(string.IsNullOrWhiteSpace(availability.ConnectionString));

        var databaseName = $"cditall_live_process_{Guid.NewGuid():N}"[..30];
        await CreateDatabaseAsync(availability.ConnectionString!, databaseName);

        try
        {
            var processSmoke = await RunLiveProcessSmokeAsync(
                availability.ConnectionString!,
                databaseName,
                model,
                timeoutSeconds,
                maxTotalTokens);

            Assert.Equal(model, processSmoke.ExecutionModel);
            Assert.InRange(processSmoke.TotalTokens, 1, maxTotalTokens);
        }
        finally
        {
            await DropDatabaseAsync(availability.ConnectionString!, databaseName);
        }
    }

    private static async Task<LiveProcessSmokeResult> RunLiveProcessSmokeAsync(
        string adminConnectionString,
        string databaseName,
        string model,
        int timeoutSeconds,
        int maxTotalTokens)
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-live-process-run-openai");
        var profile = testEnvironment.CreatePostgreSqlProfile(
            "live-process-run-openai-postgres",
            BuildDatabaseConnectionString(adminConnectionString, databaseName));
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = profile
        });
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();

        var provider = await ConfigureOpenAiProviderAsync(workspaceService, model, timeoutSeconds);
        var agentPartyId = await CreateBoundAgentAsync(aiAgentService, provider, model);
        var projectId = await CreateProjectAsync(projectsService);
        var processDefinition = BuildLiveProcessDefinition(projectId);
        var definitionId = await SaveAndPublishProcessAsync(processesService, processDefinition.Editor);
        var runId = await StartProcessRunAsync(processesService, definitionId, projectId);

        var assignmentResult = await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runId,
                RoleRequirementId = processDefinition.RoleId,
                PartyId = agentPartyId,
                DisplayName = "Live process OpenAI smoke agent",
                ExecutorKind = ProcessExecutorKindNames.AiAgent,
                BindingReason = "Live process-run smoke binds a CRM-HR AI party to its AgentFramework technical agent.",
                AllowsDirectMessaging = true
            });
        Assert.True(assignmentResult.IsSuccess, ToErrorMessage(assignmentResult.Errors));

        var step = Assert.Single(await processesService.ListStepRunsAsync(runId));
        using var dispatchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 30));
        await dispatchService.DispatchAsync(
            runId,
            step.Id,
            "live-process-openai-smoke",
            cancellationToken: dispatchTimeout.Token);

        var runDetails = await processesService.GetRunDetailsAsync(runId);
        var completedStep = Assert.Single(runDetails.StepRuns);
        var assignment = Assert.Single(runDetails.Assignments);
        Assert.Equal(ProcessStepRunStatus.Completed, completedStep.Status);
        Assert.Equal(ProcessExecutorKindNames.AiAgent, assignment.ExecutorKind);
        Assert.Equal(agentPartyId, assignment.PartyId);

        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: runId.ToString("D"),
                ProcessStepId: step.Id.ToString("D"),
                Take: 20),
            dispatchTimeout.Token);
        var executionRun = Assert.Single(
            executionRuns,
            item => string.Equals(item.RequestedBy, ProcessRunAutomationDispatchService.AutomationActor, StringComparison.Ordinal));
        var executionDetail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, dispatchTimeout.Token);

        Assert.Equal(ExecutionState.Completed, executionDetail.Run.State);
        Assert.Equal(RunOutcome.Succeeded, executionDetail.Run.Outcome);
        Assert.Equal("process-step", executionDetail.Run.SourceKind);
        Assert.Equal(step.Id.ToString("D"), executionDetail.Run.SourceId);
        Assert.Equal(runId.ToString("D"), executionDetail.Run.ProcessRunId);
        Assert.Equal(step.Id.ToString("D"), executionDetail.Run.ProcessStepId);
        Assert.Equal(ProcessRunAutomationDispatchService.AutomationActor, executionDetail.Run.RequestedBy);
        Assert.Equal(provider.Name, executionDetail.Run.ProviderName);
        Assert.Equal(model, executionDetail.Run.Model);

        var processUsage = executionDetail.UsageObservations
            .Where(item =>
                string.Equals(item.ProcessRunId, runId.ToString("D"), StringComparison.Ordinal) &&
                string.Equals(item.ProcessStepId, step.Id.ToString("D"), StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(processUsage);

        var totalTokens = processUsage.Sum(item => Math.Max(0, item.TotalTokens));
        Assert.InRange(totalTokens, 1, maxTotalTokens);
        Assert.All(processUsage, usage =>
        {
            Assert.Equal(executionDetail.Run.Id, usage.ExecutionRunId);
            Assert.Equal(ProviderKind.OpenAi, usage.ProviderKind);
            Assert.Equal(model, usage.Model);
        });

        return new LiveProcessSmokeResult(executionDetail.Run.Model, totalTokens);
    }

    private static async Task<ProviderProfile> ConfigureOpenAiProviderAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        string model,
        int timeoutSeconds)
    {
        var providers = await workspaceService.ListProvidersAsync();
        var provider = providers.FirstOrDefault(item =>
                string.Equals(item.Name, ManagedSeedProviderFallbacks.OpenAiDefaultProviderName, StringComparison.Ordinal) &&
                item.Kind == ProviderKind.OpenAi &&
                item.Transport == ProviderTransportKind.Responses &&
                item.Purpose == ProviderProfilePurpose.Chat)
            ?? throw new InvalidOperationException("The managed-seed OpenAI default chat provider was not found.");

        var editor = await workspaceService.GetProviderEditorAsync(provider.Id);
        editor.DefaultModel = model;
        editor.ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
        editor.Transport = ProviderTransportKind.Responses;
        editor.SupportsTools = true;
        editor.SupportsBackgroundResponses = true;
        editor.ConfigurationJson = WriteTimeoutSeconds(editor.ConfigurationJson, timeoutSeconds);
        if (!editor.SuggestedModels.Contains(model, StringComparer.OrdinalIgnoreCase))
        {
            editor.SuggestedModels.Add(model);
        }

        await workspaceService.SaveProviderAsync(editor);
        return (await workspaceService.ListProvidersAsync()).Single(item => item.Id == provider.Id);
    }

    private static async Task<Guid> CreateBoundAgentAsync(
        AiAgentService aiAgentService,
        ProviderProfile provider,
        string model)
    {
        var createResult = await aiAgentService.CreateAgentAsync(
            "Live process OpenAI smoke agent",
            "LIVE-PROC-OAI",
            "Executes the live process-run OpenAI smoke validation.",
            "integration-tests");
        Assert.True(createResult.IsSuccess, ToErrorMessage(createResult.Errors));

        var saveResult = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = createResult.Value,
            ProviderProfileId = provider.Id,
            DefaultModel = model,
            ExecutionMode = AiExecutionMode.Remote,
            ValidationStatus = AiValidationStatus.Approved,
            Notes = "Only used by the opt-in live process-run OpenAI smoke validation.",
            LastChangedBy = "integration-tests",
            Capabilities =
            [
                new AiCapabilityEditorModel
                {
                    Name = "Process smoke validation",
                    Scope = "Complete a single bounded process step.",
                    ToolAccess = "Required process finalizer only.",
                    Limitations = "No workspace mutation or external browsing.",
                    Notes = "Live proof must stay tied to ProcessRun automation dispatch."
                }
            ]
        });
        Assert.True(saveResult.IsSuccess, ToErrorMessage(saveResult.Errors));
        return createResult.Value;
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Live process OpenAI smoke project",
            Description = "Project scope for a live process-run OpenAI smoke validation.",
            Objective = "Prove ProcessRun automation dispatch invokes a live OpenAI-backed AgentFramework agent.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess, ToErrorMessage(result.Errors));
        return result.Value;
    }

    private static LiveProcessDefinition BuildLiveProcessDefinition(Guid projectId)
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new LiveProcessDefinition(
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "Live process OpenAI smoke definition",
                Summary = "Runs one direct AI-agent process step through the process automation dispatcher.",
                ValueStatement = "Live provider proof remains process-run grounded and cost bounded.",
                CustomerName = "Integration validation",
                OwnerName = "Integration tests",
                GovernancePolicySummary = "Opt-in only; API key values are never logged; timeout and token budget are bounded.",
                ChangeSummary = "Live smoke validation definition.",
                ConstitutionRuleSummary = "Do not claim workspace-only agent validation as process-run proof.",
                OperatingModeSummary = "Assisted execution.",
                SimulationReadinessSummary = "Live validation requires explicit environment flags.",
                Roles =
                [
                    new ProcessRoleEditorModel
                    {
                        Id = roleId,
                        Key = "live-openai-validator",
                        DisplayName = "Live OpenAI validator",
                        Purpose = "Complete the bounded live process-run smoke step.",
                        StaffingIntent = "Use the CRM-HR AI party bound to an OpenAI AgentFramework technical agent.",
                        PreferredExecutorKind = ProcessExecutorKindNames.AiAgent,
                        DefaultAllocationPercent = 100
                    }
                ],
                Steps =
                [
                    new ProcessStepEditorModel
                    {
                        Id = stepId,
                        Key = "live-process-openai-smoke",
                        Title = "Complete live process OpenAI smoke",
                        StepKind = ProcessStepKind.Work,
                        InputContractSummary = "No external tools are required; read the process work brief only.",
                        OutputContractSummary = "Use the required process finalizer with Completed status and include LIVE_PROCESS_RUN_SMOKE in the reason.",
                        EvidenceContractSummary = "AgentFramework execution run, process run id, process step id, and provider usage observation.",
                        DecisionRightsSummary = "The process dispatcher owns orchestration; the AI agent only completes this bounded step.",
                        ExceptionPolicySummary = "Provider or finalizer failure must fail the step rather than be treated as proof.",
                        TargetLeadHours = 1,
                        RoleAssignments =
                        [
                            new ProcessStepRoleRequirementEditorModel
                            {
                                Id = Guid.NewGuid(),
                                RoleRequirementId = roleId,
                                ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                                IsRequired = true
                            }
                        ]
                    }
                ]
            },
            roleId,
            stepId);
    }

    private static async Task<Guid> SaveAndPublishProcessAsync(
        ProcessesService processesService,
        ProcessDefinitionEditorModel editor)
    {
        var saveResult = await processesService.SaveAsync(editor);
        Assert.True(saveResult.IsSuccess, ToErrorMessage(saveResult.Errors));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, ToErrorMessage(publishResult.Errors));
        return saveResult.Value;
    }

    private static async Task<Guid> StartProcessRunAsync(
        ProcessesService processesService,
        Guid definitionId,
        Guid projectId)
    {
        var result = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = definitionId,
            ProjectId = projectId,
            RunName = "Live process OpenAI smoke run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Opt-in live process-run OpenAI smoke validation."
        });
        Assert.True(result.IsSuccess, ToErrorMessage(result.Errors));
        return result.Value;
    }

    private static string WriteTimeoutSeconds(string configurationJson, int timeoutSeconds)
    {
        JsonObject configuration;
        try
        {
            configuration = string.IsNullOrWhiteSpace(configurationJson)
                ? new JsonObject()
                : JsonNode.Parse(configurationJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("OpenAI provider configuration must be valid JSON before live smoke timeout can be applied.", exception);
        }

        configuration["timeoutSeconds"] = timeoutSeconds;
        return configuration.ToJsonString();
    }

    private static bool IsLiveValidationEnabled()
    {
        return IsEnabled(LiveProcessRunValidationVariable) &&
            IsEnabled(LiveOpenAiSmokeVariable);
    }

    private static bool IsEnabled(string environmentVariable)
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(environmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveTextSetting(string environmentVariable, string fallbackValue)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value)
            ? fallbackValue
            : value.Trim();
    }

    private static int ResolveBoundedIntegerSetting(
        string environmentVariable,
        int fallbackValue,
        int minimum,
        int maximum)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        if (!int.TryParse(value, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new InvalidOperationException(
                $"{environmentVariable} must be an integer between {minimum} and {maximum}.");
        }

        return parsed;
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

    private static string ToErrorMessage(IEnumerable<CanDoItAll.SharedKernel.Error> errors)
    {
        return string.Join(" | ", errors.Select(error => error.Message));
    }

    private sealed record LiveProcessDefinition(
        ProcessDefinitionEditorModel Editor,
        Guid RoleId,
        Guid StepDefinitionId);

    private sealed record LiveProcessSmokeResult(string ExecutionModel, int TotalTokens);
}
