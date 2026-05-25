using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AgentFrameworkAuditProofTests
{
    private const string ScenarioHarnessProviderBaseUrl = "scenario://harness";
    private const string ScenarioHarnessProviderName = "Scenario Harness Provider";
    private const string ScenarioHarnessOperatorName = "Scenario Harness Operator";
    private const string ScenarioHarnessModel = "scenario-local";

    private Task<ServiceProvider> BuildSeedServiceProviderAsync()
    {
        var activeProfile = CreateActiveProfile();
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(
            activeProfile.EnvironmentRootPath,
            "CanDoItAll.Tests.Playwright.AgentFrameworkAudit");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            activeProfile,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });

        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);
        services.AddScoped<NavigationManager, SeedNavigationManager>();
        return Task.FromResult(services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));
    }

    private TestDatabaseProfile CreateActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private sealed class SeedNavigationManager : NavigationManager
    {
        public SeedNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    private async Task<Guid> EnsureScenarioHarnessCatalogAsync()
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = await workspaceService.ListProvidersAsync();
        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.BaseUrl, ScenarioHarnessProviderBaseUrl, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            var providerEditor = await workspaceService.GetProviderEditorAsync();
            providerEditor.Name = ScenarioHarnessProviderName;
            providerEditor.Kind = ProviderKind.OpenAi;
            providerEditor.BaseUrl = ScenarioHarnessProviderBaseUrl;
            providerEditor.ApiKeyEnvironmentVariable = string.Empty;
            providerEditor.DefaultModel = ScenarioHarnessModel;
            providerEditor.Transport = ProviderTransportKind.Responses;
            providerEditor.IsEnabled = true;
            providerEditor.SupportsStreaming = false;
            providerEditor.SupportsTools = true;
            providerEditor.PreferFrameworkManagedChatHistory = true;
            providerEditor.SupportsBackgroundResponses = false;
            providerEditor.ConfigurationJson = "{}";
            providerEditor.Notes = "Deterministic scenario provider for integrated AgentFramework proof.";
            providerEditor.SuggestedModels =
            [
                ScenarioHarnessModel
            ];

            var providerId = await workspaceService.SaveProviderAsync(providerEditor);
            providers = await workspaceService.ListProvidersAsync();
            provider = providers.Single(item => item.Id == providerId);
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var scenarioAgent = agents.FirstOrDefault(item =>
            item.ProviderProfileId == provider.Id &&
            string.Equals(item.Name, ScenarioHarnessOperatorName, StringComparison.Ordinal));
        if (scenarioAgent is null)
        {
            var agentEditor = await workspaceService.GetAgentEditorAsync();
            agentEditor.Name = ScenarioHarnessOperatorName;
            agentEditor.RoleTitle = "Scenario Operator";
            agentEditor.Summary = "Runs deterministic AgentFramework proof scenarios through the real execution seams.";
            agentEditor.Instructions = "Run only scenario prompts and preserve durable evidence.";
            agentEditor.Status = AgentLifecycleStatus.Active;
            agentEditor.ProviderProfileId = provider.Id;
            agentEditor.Model = ScenarioHarnessModel;
            agentEditor.Workload = AgentWorkloadKind.Programming;
            agentEditor.ChatHistoryMode = AgentChatHistoryMode.ProviderDefault;
            agentEditor.Temperature = 0d;
            agentEditor.RequirePerServiceCallChatHistoryPersistence = false;
            agentEditor.EnableBackgroundResponses = false;
            agentEditor.ConfigurationJson = """{"scenarioHarness":true}""";
            agentEditor.IsTemplate = false;
            agentEditor.TemplateKey = string.Empty;
            agentEditor.Tags =
            [
                "scenario-harness",
                "agentframework-full-integration"
            ];

            await workspaceService.SaveAgentAsync(agentEditor);
        }

        return provider.Id;
    }

    private async Task<CollaborationBrowserSeed> SeedCollaborationThreadAsync()
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var collaborationService = scope.ServiceProvider.GetRequiredService<CollaborationService>();
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var subject = $"Playwright Collaboration Thread {suffix}";
        var messageBody = "Playwright seeded collaboration message for inbox and detail proof.";
        var contextLabel = $"Playwright coordination desk {suffix}";

        var threadResult = await collaborationService.CreateThreadAsync(
            new CollaborationThreadCreateRequest(
                subject,
                CollaborationContextKind.Manual,
                null,
                null,
                contextLabel,
                null,
                CollaborationInboxItemKind.Notification,
                $"playwright-thread:{suffix}",
                "Playwright Operator",
                CollaborationParticipantKind.User,
                messageBody,
                CollaborationMessageKind.Standard,
                MarkAsUnread: true));

        Assert.True(threadResult.IsSuccess, string.Join(" | ", threadResult.Errors.Select(error => error.Message)));
        return new CollaborationBrowserSeed(threadResult.Value, subject, messageBody, contextLabel);
    }

    private async Task<DirectMessagingBrowserSeed> SeedDirectMessagingRunAsync()
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Playwright Direct Messaging {suffix}");
        var definitionFixture = BuildDirectMessagingDefinitionEditor(projectId);
        var saveResult = await processesService.SaveAsync(definitionFixture.Editor);

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = $"Playwright Direct Messaging Run {suffix}",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Playwright direct messaging proof"
            });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        var sourcePartyId = await CreatePartyAsync(
            partyDirectoryService,
            $"Delivery lead {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"delivery.{suffix}@example.test");
        var targetPartyId = await CreatePartyAsync(
            partyDirectoryService,
            $"Review lead {suffix}",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"review.{suffix}@example.test");

        Assert.True((await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runResult.Value,
                RoleRequirementId = definitionFixture.SourceRoleRequirementId,
                PartyId = sourcePartyId,
                DisplayName = "Delivery lead",
                ExecutorKind = "person",
                BindingReason = "Playwright direct messaging source.",
                IsFallback = false,
                AllowsDirectMessaging = true
            })).IsSuccess);
        Assert.True((await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runResult.Value,
                RoleRequirementId = definitionFixture.TargetRoleRequirementId,
                PartyId = targetPartyId,
                DisplayName = "Review lead",
                ExecutorKind = "person",
                BindingReason = "Playwright direct messaging target.",
                IsFallback = false,
                AllowsDirectMessaging = true
            })).IsSuccess);

        return new DirectMessagingBrowserSeed(
            projectId,
            saveResult.Value,
            runResult.Value,
            definitionFixture.SourceRoleRequirementId,
            definitionFixture.TargetRoleRequirementId);
    }

    private async Task UpdateDirectMessagingPermissionAsync(
        Guid runId,
        Guid roleRequirementId,
        string displayName,
        bool allowsDirectMessaging)
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var existingAssignment = (await processesService.ListAssignmentsAsync(runId))
            .Single(item => item.RoleRequirementId == roleRequirementId && item.StepDefinitionId is null);

        var updateResult = await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runId,
                RoleRequirementId = roleRequirementId,
                PartyId = existingAssignment.PartyId,
                DisplayName = string.IsNullOrWhiteSpace(existingAssignment.DisplayName)
                    ? displayName
                    : existingAssignment.DisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(existingAssignment.ExecutorKind)
                    ? "person"
                    : existingAssignment.ExecutorKind,
                BindingReason = $"Updated direct-messaging permission for {displayName}.",
                IsFallback = existingAssignment.IsFallback,
                AllowsDirectMessaging = allowsDirectMessaging
            });

        Assert.True(updateResult.IsSuccess, string.Join(" | ", updateResult.Errors.Select(error => error.Message)));
    }

    private async Task<AgentRecoveryBrowserSeed> SeedAgentRecoveryRunAsync()
    {
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Playwright Agent Recovery {suffix}");
        var definitionFixture = BuildAgentRecoveryDefinitionEditor(projectId);
        var saveResult = await processesService.SaveAsync(definitionFixture.Editor);

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = $"Playwright Agent Recovery Run {suffix}",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Playwright agent recovery proof"
            });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        var agentPartyId = await CreatePartyAsync(
            partyDirectoryService,
            $"Recovery agent {suffix}",
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            $"recovery.agent.{suffix}@example.test");
        var assignmentResult = await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runResult.Value,
                RoleRequirementId = definitionFixture.AgentRoleRequirementId,
                PartyId = agentPartyId,
                DisplayName = "Recovery agent",
                ExecutorKind = "AI agent",
                BindingReason = "Playwright recovery proof binds an AI-owned process step.",
                IsFallback = false,
                AllowsDirectMessaging = true
            });

        Assert.True(assignmentResult.IsSuccess, string.Join(" | ", assignmentResult.Errors.Select(error => error.Message)));

        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value));
        var startResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = "Start agent-owned work for Playwright recovery proof.",
                DecidedBy = "playwright-tests"
            });

        Assert.True(startResult.IsSuccess, string.Join(" | ", startResult.Errors.Select(error => error.Message)));

        stepRun = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value));
        var blockResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = $"Required artifacts still missing: {definitionFixture.ArtifactTitle}.",
                DecidedBy = "playwright-tests"
            });

        Assert.True(blockResult.IsSuccess, string.Join(" | ", blockResult.Errors.Select(error => error.Message)));

        var now = DateTimeOffset.UtcNow;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Set<ProcessOutboxRecord>().AddAsync(
                new ProcessOutboxRecord
                {
                    ProjectId = projectId,
                    ProcessDefinitionId = saveResult.Value,
                    ProcessRunId = runResult.Value,
                    CommandKey = "dispatch-run-automation",
                    PayloadJson = BuildAutomationDispatchPayloadJson(runResult.Value, stepRun.Id, "dead-letter-proof"),
                    Status = ProcessOutboxRecordStatus.DeadLettered,
                    AttemptCount = 3,
                    LastAttemptAtUtc = now,
                    LastError = "Provider execution failed after retry exhaustion.",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            await dbContext.SaveChangesAsync();
        }

        return new AgentRecoveryBrowserSeed(
            projectId,
            saveResult.Value,
            runResult.Value,
            definitionFixture.StepTitle,
            definitionFixture.ArtifactTitle);
    }

    private async Task<WorkflowScenarioSeed> SeedWorkflowDeliveryScenarioAsync()
    {
        var providerId = await EnsureScenarioHarnessCatalogAsync();
        await using var serviceProvider = await BuildSeedServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"SC11 Workflow Delivery {suffix}");
        var managerName = $"SC11 Manager {suffix}";
        var builderAgentName = $"SC11 Workflow Builder {suffix}";
        var reviewerAgentName = $"SC11 Workflow Reviewer {suffix}";

        var managerPartyId = await CreatePartyAsync(
            partyDirectoryService,
            managerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"manager.{suffix}@example.test");
        var builderPartyId = await CreatePartyAsync(
            partyDirectoryService,
            builderAgentName,
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            $"builder.{suffix}@example.test");
        var reviewerPartyId = await CreatePartyAsync(
            partyDirectoryService,
            reviewerAgentName,
            PartyType.AiAgent,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            $"reviewer.{suffix}@example.test");

        await SaveAssignmentAsync(projectPartyBridge, projectId, managerPartyId, ProjectPartyAssignmentRole.Manager, "sc11-manager", 100m, true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, builderPartyId, ProjectPartyAssignmentRole.AiAgent, "sc11-builder", 100m, true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, reviewerPartyId, ProjectPartyAssignmentRole.AiAgent, "sc11-reviewer", 100m, false);

        var builderProfileResult = await aiAgentService.SaveAgentProfileAsync(
            new AiAgentProfileEditorModel
            {
                PartyId = builderPartyId,
                ProviderProfileId = providerId,
                DefaultModel = ScenarioHarnessModel,
                ExecutionMode = AiExecutionMode.ThirdParty,
                OwnerPartyId = managerPartyId,
                ValidationStatus = AiValidationStatus.Approved,
                Notes = "Runs SC03 through the scenario harness for workflow generation.",
                LastChangedBy = "playwright-tests",
                Capabilities =
                [
                    new AiCapabilityEditorModel
                    {
                        Name = "SC03 Workflow generation",
                        Scope = "Generate and build a Blazor workflow through the controlled scenario harness.",
                        ToolAccess = "Scenario harness provider",
                        Limitations = "Use only deterministic scenario prompts.",
                        Notes = "Bound for integrated SC11 proof."
                    }
                ]
            });
        Assert.True(builderProfileResult.IsSuccess, string.Join(" | ", builderProfileResult.Errors.Select(error => error.Message)));

        var reviewerProfileResult = await aiAgentService.SaveAgentProfileAsync(
            new AiAgentProfileEditorModel
            {
                PartyId = reviewerPartyId,
                ProviderProfileId = providerId,
                DefaultModel = ScenarioHarnessModel,
                ExecutionMode = AiExecutionMode.ThirdParty,
                OwnerPartyId = managerPartyId,
                ValidationStatus = AiValidationStatus.Approved,
                Notes = "Runs SC10 through the scenario harness for workflow review.",
                LastChangedBy = "playwright-tests",
                Capabilities =
                [
                    new AiCapabilityEditorModel
                    {
                        Name = "SC10 Workflow review",
                        Scope = "Review a generated Blazor workflow delivery and produce evidence.",
                        ToolAccess = "Scenario harness provider",
                        Limitations = "Use only deterministic scenario prompts.",
                        Notes = "Bound for integrated SC11 proof."
                    }
                ]
            });
        Assert.True(reviewerProfileResult.IsSuccess, string.Join(" | ", reviewerProfileResult.Errors.Select(error => error.Message)));

        var definitionFixture = BuildWorkflowDeliveryDefinitionEditor(projectId);
        var saveResult = await processesService.SaveAsync(definitionFixture.Editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        return new WorkflowScenarioSeed(
            projectId,
            saveResult.Value,
            managerName,
            builderPartyId,
            reviewerPartyId,
            builderAgentName,
            reviewerAgentName,
            definitionFixture.BuilderRoleRequirementId,
            definitionFixture.ReviewerRoleRequirementId,
            definitionFixture.BuilderRoleName,
            definitionFixture.ReviewerRoleName,
            definitionFixture.GenerationStepTitle,
            definitionFixture.HandoffStepTitle,
            definitionFixture.ReviewStepTitle);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(
            new ProjectEditorModel
            {
                Name = name,
                Description = $"{name} description",
                Objective = $"{name} objective",
                CurrentPhase = "Execution"
            });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = partyType,
                LifecycleStatus = lifecycleStatus,
                DisplayName = displayName,
                Summary = $"{displayName} summary",
                LastChangedBy = "playwright-tests",
                Roles =
                [
                    new PartyRoleAssignmentEditorModel
                    {
                        RoleKind = roleKind,
                        Title = roleKind.ToString(),
                        IsPrimary = true
                    }
                ],
                ContactPoints =
                [
                    new PartyContactPointEditorModel
                    {
                        ContactType = PartyContactType.Email,
                        Label = "Primary email",
                        Value = email,
                        NormalizedValue = email.ToLowerInvariant(),
                        IsPrimary = true,
                        IsPublic = true
                    }
                ]
            });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task SaveAssignmentAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        Guid projectId,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        string nodeKey,
        decimal allocationPercent,
        bool isPrimary)
    {
        var result = await projectPartyBridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = partyId,
                Role = role,
                NodeKey = nodeKey,
                AllocationPercent = allocationPercent,
                StartsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)),
                Notes = $"Playwright assignment for {role}",
                IsPrimary = isPrimary,
                Source = "playwright-tests"
            });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
    }

    private static string BuildAutomationDispatchPayloadJson(Guid runId, Guid stepRunId, string trigger)
    {
        return JsonSerializer.Serialize(
            new
            {
                searchUpsert = (object?)null,
                searchDelete = (object?)null,
                activity = (object?)null,
                automationDispatch = new
                {
                    processRunId = runId,
                    stepRunId,
                    trigger
                }
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
