using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class AgentShowcaseCalculatorSeeder(
    ILogger<AgentShowcaseCalculatorSeeder> logger,
    ScenarioSeederOptions options,
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    ProcessesService processesService,
    ProcessTemplateProjectionService projectionService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    AiAgentService aiAgentService,
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const string ProjectName = "Showcase - Blazor SSR calculator delivery";
    private const string DefinitionName = "Showcase - Blazor SSR calculator process";
    private const string Marker = "scenario:blazor-ssr-calculator-showcase";
    private const string ProcessTemplateKey = "software-delivery";
    private const string ShowcaseRootRelativePath = "showcases/blazor-ssr-calculator";
    private const string ShowcaseArtifactRootRelativePath = "artifacts/showcases/blazor-ssr-calculator";
    private const string AppParentRelativePath = ShowcaseRootRelativePath + "/app";
    private const string AppProjectRelativePath = AppParentRelativePath + "/SimpleCalculatorApp/SimpleCalculatorApp.csproj";
    private const string AppProgramRelativePath = AppParentRelativePath + "/SimpleCalculatorApp/Program.cs";
    private const string AppHomeRelativePath = AppParentRelativePath + "/SimpleCalculatorApp/Components/Pages/Home.razor";
    private const string BriefRelativePath = ShowcaseRootRelativePath + "/SHOWCASE-BRIEF.md";
    private const string LaunchScriptRelativePath = ShowcaseRootRelativePath + "/Launch-CalculatorApp.ps1";
    private const string StopScriptRelativePath = ShowcaseRootRelativePath + "/Stop-CalculatorApp.ps1";
    private const string ApplyAppScriptRelativePath = ShowcaseRootRelativePath + "/Apply-CalculatorShowcaseApp.ps1";
    private const string ImportPlaywrightEvidenceScriptRelativePath = ShowcaseRootRelativePath + "/Import-PlaywrightEvidence.ps1";
    private const string PlaywrightScratchRelativePath = ShowcaseRootRelativePath + "/.playwright-mcp";
    private const string EvidenceRelativePath = ShowcaseArtifactRootRelativePath + "/evidence";
    private const string ProcessEvidenceRelativePath = EvidenceRelativePath + "/process";
    private const string UiEvidenceRelativePath = EvidenceRelativePath + "/ui";
    private const string AppUrl = "http://127.0.0.1:5088";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentShowcaseSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var workspacePlan = EnsureWorkspaceAssets();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var provider = await ResolveProviderAsync(workspaceService, cancellationToken);
        var capabilityIdsByKey = await EnsureCapabilitiesAsync(workspaceService, workspacePlan, cancellationToken);
        var agentSpecs = BuildAgentSpecs(workspacePlan);
        var agentsByRoleKey = await EnsureAgentsAsync(
            workspaceService,
            provider.Id,
            capabilityIdsByKey,
            agentSpecs,
            cancellationToken);

        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        var aiDirectory = await aiAgentService.ListAgentDirectoryAsync(cancellationToken);
        var bindingsByRoleKey = ResolveRoleBindings(agentSpecs, agentsByRoleKey, aiDirectory);
        var projectId = await EnsureProjectAsync(cancellationToken);
        var graph = await EnsureProjectStructureAsync(projectId, cancellationToken);
        await EnsureProjectAssignmentsAsync(projectId, bindingsByRoleKey, cancellationToken);
        var definitionId = await EnsureProcessDefinitionAsync(projectId, workspacePlan, cancellationToken);
        await UpsertProcessBindingAsync(projectId, graph.FeatureNodeId, definitionId, null, cancellationToken);
        var launch = await CreateLaunchAndRunAsync(
            projectId,
            definitionId,
            bindingsByRoleKey,
            graph,
            workspacePlan,
            cancellationToken);
        var monitoring = await MonitorRunAsync(
            projectId,
            graph,
            definitionId,
            launch.LaunchPlanId,
            launch.RunId,
            agentsByRoleKey,
            workspacePlan,
            workspaceService,
            cancellationToken);

        logger.LogInformation(
            "Completed showcase scenario. ProjectId={ProjectId} DefinitionId={DefinitionId} LaunchPlanId={LaunchPlanId} RunId={RunId} RunStatus={RunStatus}",
            projectId,
            definitionId,
            launch.LaunchPlanId,
            launch.RunId,
            monitoring.Run.Status);

        return new AgentShowcaseSeedResult(
            projectId,
            ProjectName,
            definitionId,
            launch.LaunchPlanId,
            launch.RunId,
            monitoring.Run.Status.ToString(),
            $"/projects/{projectId:D}/processes?processId={definitionId:D}&runId={launch.RunId:D}",
            graph.FeatureNodeId,
            ShowcaseRootRelativePath,
            AppProjectRelativePath,
            BriefRelativePath,
            LaunchScriptRelativePath,
            UiEvidenceRelativePath,
            capabilityIdsByKey.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            bindingsByRoleKey.Values
                .OrderBy(item => item.RoleKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new AgentShowcaseBindingResult(
                    item.RoleKey,
                    item.AgentName,
                    item.AgentId,
                    item.PartyId,
                    item.TechnicalAgentId))
                .ToList(),
            monitoring.StepResults,
            monitoring.ExecutionResults);
    }

    private ShowcaseWorkspacePlan EnsureWorkspaceAssets()
    {
        var organizationScope = workspaceFactory.GetOrganizationScope();
        var showcaseRoot = ResolveWorkspaceFullPath(ShowcaseRootRelativePath, organizationScope);
        var appParent = ResolveWorkspaceFullPath(AppParentRelativePath, organizationScope);
        var appRoot = Path.Combine(appParent, "SimpleCalculatorApp");
        var evidenceRoot = ResolveWorkspaceFullPath(EvidenceRelativePath, organizationScope);
        var processEvidenceRoot = ResolveWorkspaceFullPath(ProcessEvidenceRelativePath, organizationScope);
        var uiEvidenceRoot = ResolveWorkspaceFullPath(UiEvidenceRelativePath, organizationScope);
        var playwrightScratchRoot = ResolveWorkspaceFullPath(PlaywrightScratchRelativePath, organizationScope);
        var pidFile = Path.Combine(uiEvidenceRoot, "calculator-app.pid");

        TryStopShowcaseProcess(pidFile);
        DeleteDirectoryIfExists(appRoot);
        DeleteDirectoryIfExists(playwrightScratchRoot);
        ResetDirectory(evidenceRoot);

        Directory.CreateDirectory(showcaseRoot);
        Directory.CreateDirectory(appParent);
        Directory.CreateDirectory(evidenceRoot);
        Directory.CreateDirectory(processEvidenceRoot);
        Directory.CreateDirectory(uiEvidenceRoot);
        Directory.CreateDirectory(playwrightScratchRoot);
        foreach (var uiValidationStepKey in UiValidationStepKeys)
        {
            Directory.CreateDirectory(Path.Combine(playwrightScratchRoot, uiValidationStepKey));
        }

        var plan = new ShowcaseWorkspacePlan(
            showcaseRoot,
            appParent,
            evidenceRoot,
            processEvidenceRoot,
            uiEvidenceRoot,
            ResolveWorkspaceFullPath(BriefRelativePath, organizationScope),
            ResolveWorkspaceFullPath(LaunchScriptRelativePath, organizationScope),
            ResolveWorkspaceFullPath(StopScriptRelativePath, organizationScope),
            ResolveWorkspaceFullPath(ApplyAppScriptRelativePath, organizationScope),
            ResolveWorkspaceFullPath(ImportPlaywrightEvidenceScriptRelativePath, organizationScope),
            playwrightScratchRoot);

        foreach (var roleFolder in BuildAgentSpecs(plan).Select(item => item.ProcessArtifactDirectoryRelativePath))
        {
            Directory.CreateDirectory(ResolveWorkspaceFullPath(roleFolder, organizationScope));
        }

        File.WriteAllText(plan.BriefFullPath, BuildShowcaseBriefContent(plan), new System.Text.UTF8Encoding(false));
        File.WriteAllText(plan.LaunchScriptFullPath, BuildLaunchScriptContent(), new System.Text.UTF8Encoding(false));
        File.WriteAllText(plan.StopScriptFullPath, BuildStopScriptContent(), new System.Text.UTF8Encoding(false));
        File.WriteAllText(plan.ApplyAppScriptFullPath, BuildApplyCalculatorAppScriptContent(), new System.Text.UTF8Encoding(false));
        File.WriteAllText(plan.ImportPlaywrightEvidenceScriptFullPath, BuildImportPlaywrightEvidenceScriptContent(), new System.Text.UTF8Encoding(false));
        return plan;
    }

    private string ResolveWorkspaceFullPath(string relativePath, WorkspaceScopeDescriptor scope)
    {
        var resolvedRelativePath = ResolveScopedManagedRelativePath(relativePath, scope);
        return Path.Combine(
            options.WorkspaceRootPath,
            resolvedRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ResolveScopedManagedRelativePath(string relativePath, WorkspaceScopeDescriptor scope)
    {
        if (!relativePath.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        var artifactSuffix = relativePath["artifacts/".Length..];
        return scope.CombineArtifactPath(artifactSuffix);
    }

    private void TryStopShowcaseProcess(string pidFile)
    {
        if (!File.Exists(pidFile))
        {
            return;
        }

        try
        {
            var pidText = File.ReadAllText(pidFile).Trim();
            if (!int.TryParse(pidText, out var pid))
            {
                return;
            }

            var process = System.Diagnostics.Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            try
            {
                File.Delete(pidFile);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void ResetDirectory(string path)
    {
        DeleteDirectoryIfExists(path);

        Directory.CreateDirectory(path);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private async Task<ProviderProfile> ResolveProviderAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        return providers.FirstOrDefault(item =>
                   item.IsEnabled &&
                   item.Kind == ProviderKind.OpenAi &&
                   item.Transport == ProviderTransportKind.ChatCompletions)
               ?? providers.FirstOrDefault(item =>
                   item.IsEnabled &&
                   item.Kind == ProviderKind.OpenAi &&
                   item.Transport == ProviderTransportKind.Responses)
               ?? providers.FirstOrDefault(item =>
                   item.IsEnabled &&
                   item.Kind == ProviderKind.OpenAi)
               ?? providers.FirstOrDefault(item => item.IsEnabled)
               ?? throw new InvalidOperationException("No enabled provider profile is available for the showcase scenario.");
    }

    private async Task<Dictionary<string, Guid>> EnsureCapabilitiesAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        ShowcaseWorkspacePlan workspacePlan,
        CancellationToken cancellationToken)
    {
        var existing = (await workspaceService.ListCapabilitiesAsync(cancellationToken))
            .ToDictionary(item => item.Key, item => item, StringComparer.OrdinalIgnoreCase);

        foreach (var spec in BuildCapabilitySpecs(workspacePlan))
        {
            CapabilityEditorModel editor;
            if (existing.TryGetValue(spec.Key, out var current))
            {
                editor = await workspaceService.GetCapabilityEditorAsync(current.Id, cancellationToken);
            }
            else
            {
                editor = await workspaceService.GetCapabilityEditorAsync(cancellationToken: cancellationToken);
            }

            editor.Kind = spec.Kind;
            editor.Key = spec.Key;
            editor.Name = spec.Name;
            editor.Description = spec.Description;
            editor.EndpointOrPath = spec.EndpointOrPath;
            editor.ConfigurationJson = spec.ConfigurationJson;
            editor.IsBuiltIn = false;
            var capabilityId = await workspaceService.SaveCapabilityAsync(editor, cancellationToken);
            existing[spec.Key] = new CapabilityCatalogItem(
                capabilityId,
                spec.Kind,
                spec.Key,
                spec.Name,
                spec.Description,
                spec.EndpointOrPath,
                spec.ConfigurationJson,
                CapabilityProofStatus.NotRun,
                string.Empty,
                null,
                false);
        }

        var refreshed = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        return refreshed.ToDictionary(item => item.Key, item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, AgentDefinition>> EnsureAgentsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        Guid providerId,
        IReadOnlyDictionary<string, Guid> capabilityIdsByKey,
        IReadOnlyList<ShowcaseAgentSpec> agentSpecs,
        CancellationToken cancellationToken)
    {
        var existingAgents = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .ToDictionary(item => item.TemplateKey, item => item, StringComparer.OrdinalIgnoreCase);
        var results = new Dictionary<string, AgentDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in agentSpecs)
        {
            AgentEditorModel editor;
            if (existingAgents.TryGetValue(spec.TemplateKey, out var existing))
            {
                editor = await workspaceService.GetAgentEditorAsync(existing.Id, cancellationToken);
            }
            else
            {
                editor = await workspaceService.GetAgentEditorAsync(cancellationToken: cancellationToken);
            }

            editor.Name = spec.Name;
            editor.RoleTitle = spec.RoleTitle;
            editor.Summary = spec.Summary;
            editor.Instructions = spec.Instructions;
            editor.Status = AgentLifecycleStatus.Active;
            editor.ProviderProfileId = providerId;
            editor.Model = spec.Model;
            editor.Workload = spec.Workload;
            editor.ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged;
            editor.Temperature = 0.1d;
            editor.RequirePerServiceCallChatHistoryPersistence = false;
            editor.EnableBackgroundResponses = false;
            editor.ConfigurationJson = """{"enableCompaction":true,"slidingWindowTurns":10,"maxLocalRagResults":5}""";
            editor.IsTemplate = false;
            editor.TemplateKey = spec.TemplateKey;
            editor.Permissions = AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                CanAskOtherAgents = false,
                CanEscalateToHuman = true,
                CanObserveOtherAgents = false,
                CanScheduleWork = false,
                RequiresApprovalForExternalCalls = true,
                AutoApproveExternalCallsByDefault = true
            };
            editor.SelectedCapabilityIds = spec.CapabilityKeys
                .Select(key => capabilityIdsByKey.TryGetValue(key, out var capabilityId)
                    ? capabilityId
                    : throw new InvalidOperationException($"Capability '{key}' is missing from the showcase workspace catalog."))
                .Distinct()
                .ToList();
            editor.Tags =
            [
                "showcase",
                "blazor-ssr-calculator",
                spec.RoleKey
            ];

            var agentId = await workspaceService.SaveAgentAsync(editor, cancellationToken);
            var refreshedAgents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
            var savedAgent = refreshedAgents.Single(item => item.Id == agentId);
            existingAgents[savedAgent.TemplateKey] = savedAgent;
            results[spec.RoleKey] = savedAgent;
        }

        return results;
    }

    private Dictionary<string, ShowcaseAgentBinding> ResolveRoleBindings(
        IReadOnlyList<ShowcaseAgentSpec> agentSpecs,
        IReadOnlyDictionary<string, AgentDefinition> agentsByRoleKey,
        IReadOnlyList<AiAgentListItemModel> aiDirectory)
    {
        var aiDirectoryByTechnicalId = aiDirectory
            .Where(item => item.TechnicalAgentId.HasValue)
            .ToDictionary(item => item.TechnicalAgentId!.Value, item => item);
        var bindings = new Dictionary<string, ShowcaseAgentBinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in agentSpecs)
        {
            if (!agentsByRoleKey.TryGetValue(spec.RoleKey, out var agent))
            {
                throw new InvalidOperationException($"Showcase agent for role '{spec.RoleKey}' was not saved.");
            }

            if (!aiDirectoryByTechnicalId.TryGetValue(agent.Id, out var directoryEntry))
            {
                throw new InvalidOperationException(
                    $"CRM-HR projection did not materialize the organization agent '{agent.Name}' into the AI directory.");
            }

            if (!directoryEntry.TechnicalAgentId.HasValue ||
                directoryEntry.BindingStatus != AiResourceBindingStatus.Bound)
            {
                throw new InvalidOperationException(
                    $"AI directory entry '{directoryEntry.DisplayName}' is not fully bound. BindingStatus={directoryEntry.BindingStatus}.");
            }

            bindings[spec.RoleKey] = new ShowcaseAgentBinding(
                spec.RoleKey,
                agent.Name,
                agent.Id,
                directoryEntry.PartyId,
                directoryEntry.TechnicalAgentId.Value);
        }

        return bindings;
    }

    private async Task<Guid> EnsureProjectAsync(CancellationToken cancellationToken)
    {
        var existing = (await projectsService.ListAsync(cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Name, ProjectName, StringComparison.Ordinal));
        var saveResult = await projectsService.SaveAsync(
            new ProjectEditorModel
            {
                Id = existing?.Id,
                Name = ProjectName,
                Description = """
                    End-to-end showcase for the first agent-integration wave.

                    Marker: scenario:blazor-ssr-calculator-showcase
                    """,
                Objective = """
                    Use the shared organization agent catalog as the source of truth, import the template-driven software delivery process, launch exact AI role assignments, deliver a static SSR Blazor calculator, validate it through Playwright, and capture the defects that still block a production-grade agentic workflow.
                    """,
                Status = ProjectStatus.Active,
                CurrentPhase = "Showcase automation execution"
            },
            cancellationToken);
        return EnsureSuccess(saveResult);
    }

    private sealed record ShowcaseCapabilitySpec(
        CapabilityKind Kind,
        string Key,
        string Name,
        string Description,
        string EndpointOrPath,
        string ConfigurationJson);

    private sealed record ShowcaseAgentSpec(
        string RoleKey,
        string Name,
        string RoleTitle,
        string Summary,
        string Instructions,
        string TemplateKey,
        string Model,
        AgentWorkloadKind Workload,
        string ProcessArtifactDirectoryRelativePath,
        IReadOnlyList<string> CapabilityKeys);

    private sealed record ShowcaseAgentBinding(
        string RoleKey,
        string AgentName,
        Guid AgentId,
        Guid PartyId,
        Guid TechnicalAgentId);

    private sealed record ShowcaseWorkspacePlan(
        string ShowcaseRootFullPath,
        string AppParentFullPath,
        string EvidenceFullPath,
        string ProcessEvidenceFullPath,
        string UiEvidenceFullPath,
        string BriefFullPath,
        string LaunchScriptFullPath,
        string StopScriptFullPath,
        string ApplyAppScriptFullPath,
        string ImportPlaywrightEvidenceScriptFullPath,
        string PlaywrightScratchFullPath);

    private sealed record ShowcaseGraph(
        string PhaseNodeId,
        string DeliveryBlockNodeId,
        string FeatureNodeId);

    private sealed record ShowcaseLaunchResult(
        Guid LaunchPlanId,
        Guid RunId);

    private sealed record ShowcaseMonitoringResult(
        ProcessRunListItem Run,
        IReadOnlyList<AgentShowcaseStepResult> StepResults,
        IReadOnlyList<AgentShowcaseExecutionResult> ExecutionResults);
}

internal sealed record AgentShowcaseSeedResult(
    Guid ProjectId,
    string ProjectName,
    Guid DefinitionId,
    Guid LaunchPlanId,
    Guid RunId,
    string RunStatus,
    string ProcessRoute,
    string FeatureNodeId,
    string ShowcaseRootRelativePath,
    string AppProjectRelativePath,
    string BriefRelativePath,
    string LaunchScriptRelativePath,
    string UiEvidenceRelativePath,
    IReadOnlyList<string> CapabilityKeys,
    IReadOnlyList<AgentShowcaseBindingResult> Agents,
    IReadOnlyList<AgentShowcaseStepResult> Steps,
    IReadOnlyList<AgentShowcaseExecutionResult> ExecutionRuns);

internal sealed record AgentShowcaseBindingResult(
    string RoleKey,
    string AgentName,
    Guid AgentId,
    Guid PartyId,
    Guid TechnicalAgentId);

internal sealed record AgentShowcaseStepResult(
    int Sequence,
    string Title,
    string Status,
    string CurrentExecutorName,
    string DecisionSummary,
    string BlockedReason,
    string RefusalReason);

internal sealed record AgentShowcaseExecutionResult(
    Guid ExecutionRunId,
    string StepTitle,
    string AgentName,
    string State,
    string Outcome,
    int ApprovalCount,
    int ArtifactCount,
    string ResultSummary);
