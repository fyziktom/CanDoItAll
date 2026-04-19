using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class UnitsConverterDeliveryProvisioningSeeder(
    ILogger<UnitsConverterDeliveryProvisioningSeeder> logger,
    ScenarioSeederOptions options,
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    ProcessesService processesService,
    HrService hrService,
    ProcessTemplateProjectionService projectionService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    PartyDirectoryService partyDirectoryService,
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const string ProjectName = "Blazor SSR basic units converter";
    private const string DefinitionName = "Basic units converter delivery";
    private const string LegacyMarker = "scenario:blazor-ssr-basic-units-converter";
    private const string Marker = "scenario:units-converter-delivery";
    private const string ProcessTemplateKey = "software-delivery";
    private const string HumanPartyDisplayName = "Human Delivery Steward";
    private const string LegacyDeliveryRootRelativePath = "deliveries/blazor-ssr-basic-units-converter";
    private const string LegacyDeliveryArtifactRootRelativePath = "artifacts/deliveries/blazor-ssr-basic-units-converter";
    private const string LegacySolutionRelativePath = LegacyDeliveryRootRelativePath + "/BasicUnitsConverter.slnx";
    private const string LegacyPreSlnxSolutionRelativePath = LegacyDeliveryRootRelativePath + "/BasicUnitsConverter.sln";
    private const string LegacyCoreProjectRelativePath = LegacyDeliveryRootRelativePath + "/src/BasicUnitsConverter.Core/BasicUnitsConverter.Core.csproj";
    private const string LegacyWebProjectRelativePath = LegacyDeliveryRootRelativePath + "/src/BasicUnitsConverter.Web/BasicUnitsConverter.Web.csproj";
    private const string LegacyTestsProjectRelativePath = LegacyDeliveryRootRelativePath + "/tests/BasicUnitsConverter.Core.Tests/BasicUnitsConverter.Core.Tests.csproj";
    private const string LegacyWebProgramRelativePath = LegacyDeliveryRootRelativePath + "/src/BasicUnitsConverter.Web/Program.cs";
    private const string LegacyWebHomeRelativePath = LegacyDeliveryRootRelativePath + "/src/BasicUnitsConverter.Web/Components/Pages/Home.razor";
    private const string LegacyBootstrapScriptRelativePath = LegacyDeliveryRootRelativePath + "/Bootstrap-UnitsConverterSolution.ps1";
    private const string LegacyLaunchScriptRelativePath = LegacyDeliveryRootRelativePath + "/Launch-UnitsConverterApp.ps1";
    private const string LegacyStopScriptRelativePath = LegacyDeliveryRootRelativePath + "/Stop-UnitsConverterApp.ps1";
    private const string LegacyImportPlaywrightEvidenceScriptRelativePath = LegacyDeliveryRootRelativePath + "/Import-PlaywrightEvidence.ps1";
    private const string LegacyPlaywrightScratchRelativePath = LegacyDeliveryRootRelativePath + "/.playwright-mcp";
    private const string DeliveryRootRelativePath = "deliveries/units-converter";
    private const string DeliveryArtifactRootRelativePath = "artifacts/deliveries/units-converter";
    private const string SolutionRelativePath = DeliveryRootRelativePath + "/Units.slnx";
    private const string WebProjectRelativePath = DeliveryRootRelativePath + "/src/Units.Web/Units.Web.csproj";
    private const string CoreProjectRelativePath = DeliveryRootRelativePath + "/src/Units.Core/Units.Core.csproj";
    private const string TestsProjectRelativePath = DeliveryRootRelativePath + "/tests/Units.Tests/Units.Tests.csproj";
    private const string WebProgramRelativePath = DeliveryRootRelativePath + "/src/Units.Web/Program.cs";
    private const string WebHomeRelativePath = DeliveryRootRelativePath + "/src/Units.Web/Components/Pages/Home.razor";
    private const string WebMainLayoutRelativePath = DeliveryRootRelativePath + "/src/Units.Web/Components/Layout/MainLayout.razor";
    private const string WebNavMenuRelativePath = DeliveryRootRelativePath + "/src/Units.Web/Components/Layout/NavMenu.razor";
    private const string WebCssRelativePath = DeliveryRootRelativePath + "/src/Units.Web/wwwroot/app.css";
    private const string CoreServiceRelativePath = DeliveryRootRelativePath + "/src/Units.Core/Conversions/UnitConversionService.cs";
    private const string CoreModelRelativePath = DeliveryRootRelativePath + "/src/Units.Core/Conversions/UnitCatalog.cs";
    private const string TestsFileRelativePath = DeliveryRootRelativePath + "/tests/Units.Tests/UnitConversionServiceTests.cs";
    private const string BriefRelativePath = DeliveryRootRelativePath + "/PROJECT-BRIEF.md";
    private const string BootstrapScriptRelativePath = DeliveryRootRelativePath + "/Bootstrap-UnitsSolution.ps1";
    private const string LaunchScriptRelativePath = DeliveryRootRelativePath + "/Launch-UnitsApp.ps1";
    private const string StopScriptRelativePath = DeliveryRootRelativePath + "/Stop-UnitsApp.ps1";
    private const string ImportPlaywrightEvidenceScriptRelativePath = DeliveryRootRelativePath + "/Import-PlaywrightEvidence.ps1";
    private const string PlaywrightScratchRelativePath = DeliveryRootRelativePath + "/.playwright-mcp";
    private const string ProcessEvidenceRelativePath = DeliveryArtifactRootRelativePath + "/process";
    private const string UiEvidenceRelativePath = DeliveryArtifactRootRelativePath + "/ui";
    private const string AppUrl = "http://127.0.0.1:5090";

    private static readonly DeliveryRoleRequirement[] DeliveryRoleRequirements =
    [
        new("product-owner", HumanPartyDisplayName, true),
        new("delivery-manager", HumanPartyDisplayName, true),
        new("solution-architect", "Portfolio Architect", false),
        new("lead-engineer", "Programming Workspace Analyst", false),
        new("review-lead", "Code Review Lead", false),
        new("qa-lead", "Delivery QA Observer", false),
        new("ui-review-lead", "UI Review Lead", false),
        new("security-reviewer", "Security Reviewer", false),
        new("release-manager", "Release Readiness Manager", false)
    ];

    public async Task<UnitsConverterDeliveryProvisioningResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var workspacePlan = EnsureWorkspaceAssets();
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        var humanPartyId = await EnsureHumanPartyAsync(cancellationToken);
        var bindingsByRoleKey = await ResolveRoleBindingsAsync(humanPartyId, cancellationToken);
        var skillCatalog = await EnsureScenarioSkillsAsync(bindingsByRoleKey, cancellationToken);
        var projectId = await EnsureProjectAsync(cancellationToken);
        var graph = await EnsureProjectStructureAsync(projectId, cancellationToken);
        await EnsureProjectAssignmentsAsync(projectId, bindingsByRoleKey, cancellationToken);
        var definitionId = await EnsureProcessDefinitionAsync(projectId, workspacePlan, skillCatalog, cancellationToken);
        await UpsertProcessBindingAsync(projectId, graph.DeliveryFeatureNodeId, definitionId, null, cancellationToken);
        var launch = await CreateLaunchAndRunAsync(
            projectId,
            definitionId,
            bindingsByRoleKey,
            graph,
            workspacePlan,
            cancellationToken);

        logger.LogInformation(
            "Provisioned units-converter delivery. ProjectId={ProjectId} DefinitionId={DefinitionId} LaunchPlanId={LaunchPlanId} RunId={RunId}",
            projectId,
            definitionId,
            launch.LaunchPlanId,
            launch.RunId);

        return new UnitsConverterDeliveryProvisioningResult(
            projectId,
            ProjectName,
            definitionId,
            launch.LaunchPlanId,
            launch.RunId,
            humanPartyId,
            $"/projects/{projectId:D}/processes?processId={definitionId:D}&runId={launch.RunId:D}",
            graph.ScopePhaseNodeId,
            graph.BuildPhaseNodeId,
            graph.ReleasePhaseNodeId,
            graph.DeliveryFeatureNodeId,
            SolutionRelativePath,
            WebProjectRelativePath,
            CoreProjectRelativePath,
            TestsProjectRelativePath,
            BriefRelativePath,
            BootstrapScriptRelativePath,
            LaunchScriptRelativePath,
            UiEvidenceRelativePath,
            bindingsByRoleKey.Values
                .OrderBy(item => item.RoleKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new UnitsConverterRoleBindingResult(
                    item.RoleKey,
                    item.DisplayName,
                    item.PartyId,
                    item.TechnicalAgentId,
                    item.IsHuman))
                .ToList());
    }

    private async Task<Guid> EnsureHumanPartyAsync(CancellationToken cancellationToken)
    {
        var existing = await partyDirectoryService.ListDirectoryAsync(cancellationToken);
        var party = existing.FirstOrDefault(item =>
            item.PartyType == PartyType.Person &&
            string.Equals(item.DisplayName, HumanPartyDisplayName, StringComparison.Ordinal));
        if (party is not null)
        {
            return party.Id;
        }

        var saveResult = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = PartyType.Person,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = HumanPartyDisplayName,
                Summary = "Human-controlled delivery steward for governed agent execution, scope confirmation, and release approvals.",
                Notes = "Used by the units-converter delivery scenario to keep product-owner and delivery-manager responsibilities explicitly human.",
                TimeZone = "America/La_Paz",
                LastChangedBy = "units-converter-delivery-seeder",
                Tags =
                [
                    "scenario",
                    "human",
                    "units-converter-delivery"
                ]
            },
            cancellationToken);
        return EnsureSuccess(saveResult);
    }

    private async Task<Dictionary<string, DeliveryRoleBinding>> ResolveRoleBindingsAsync(
        Guid humanPartyId,
        CancellationToken cancellationToken)
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agentsByName = agents.ToDictionary(item => item.Name, item => item, StringComparer.Ordinal);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var technicalBindings = await dbContext.Set<AiResourceBinding>()
            .Where(item => item.TechnicalAgentId.HasValue)
            .ToListAsync(cancellationToken);
        var bindingsByTechnicalAgentId = technicalBindings
            .GroupBy(item => item.TechnicalAgentId!.Value)
            .Select(group => group
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ThenByDescending(item => item.CreatedAtUtc)
                .First())
            .ToDictionary(item => item.TechnicalAgentId!.Value);
        var bindings = new Dictionary<string, DeliveryRoleBinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in DeliveryRoleRequirements)
        {
            if (requirement.IsHuman)
            {
                bindings[requirement.RoleKey] = new DeliveryRoleBinding(
                    requirement.RoleKey,
                    HumanPartyDisplayName,
                    humanPartyId,
                    null,
                    true);
                continue;
            }

            if (!agentsByName.TryGetValue(requirement.DisplayName, out var agent))
            {
                throw new InvalidOperationException(
                    $"Required AgentFramework-owned agent '{requirement.DisplayName}' for role '{requirement.RoleKey}' was not found in the organization workspace.");
            }

            Guid? partyId = null;
            if (bindingsByTechnicalAgentId.TryGetValue(agent.Id, out var binding))
            {
                partyId = binding.PartyId;
            }

            if (!partyId.HasValue)
            {
                throw new InvalidOperationException(
                    $"AgentFramework-owned agent '{requirement.DisplayName}' is missing its CRM-HR projection binding.");
            }

            bindings[requirement.RoleKey] = new DeliveryRoleBinding(
                requirement.RoleKey,
                agent.Name,
                partyId.Value,
                agent.Id,
                false);
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
                Description = $"""
                    Serious governed delivery of a Blazor SSR application that converts basic units without relying on a duplicate agent registry.

                    Marker: {Marker}
                    """,
                Objective = """
                    Create a maintainable Blazor SSR units-converter application through explicit human scope control, AgentFramework-owned delivery agents, screenshot-backed QA, and release-ready evidence recorded through the process and workbench surfaces.
                    """,
                Status = ProjectStatus.Active,
                CurrentPhase = "Provisioned for governed multi-agent delivery"
            },
            cancellationToken);
        return EnsureSuccess(saveResult);
    }

    private async Task EnsureProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyDictionary<string, DeliveryRoleBinding> bindingsByRoleKey,
        CancellationToken cancellationToken)
    {
        var existingAssignments = await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(projectId, cancellationToken);
        foreach (var binding in bindingsByRoleKey.Values)
        {
            var targetRole = binding.IsHuman
                ? binding.RoleKey switch
                {
                    "product-owner" => ProjectPartyAssignmentRole.CustomerContact,
                    "delivery-manager" => ProjectPartyAssignmentRole.Manager,
                    _ => ProjectPartyAssignmentRole.Stakeholder
                }
                : ProjectPartyAssignmentRole.AiAgent;
            var exists = existingAssignments.Any(item =>
                item.PartyId == binding.PartyId &&
                item.Role == targetRole &&
                string.IsNullOrWhiteSpace(item.NodeKey));
            if (exists)
            {
                continue;
            }

            EnsureSuccess(await projectPartyIntegrationBridge.SaveAssignmentAsync(
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    PartyId = binding.PartyId,
                    Role = targetRole,
                    IsPrimary = true,
                    AllocationPercent = binding.IsHuman ? 100m : 60m,
                    Source = "units-converter-delivery-seeder",
                    Notes = binding.IsHuman
                        ? $"Human project assignment for role '{binding.RoleKey}'."
                        : $"AgentFramework delivery assignment for role '{binding.RoleKey}'."
                },
                cancellationToken));
        }
    }

    private static Guid EnsureSuccess(Result<Guid> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }

        return result.Value;
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(FormatErrors(result.Errors));
        }
    }

    private static string FormatErrors(IReadOnlyCollection<Error> errors)
    {
        return errors.Count == 0
            ? "Unknown failure."
            : string.Join(Environment.NewLine, errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    private sealed record DeliveryRoleRequirement(string RoleKey, string DisplayName, bool IsHuman);

    private sealed record DeliveryRoleBinding(
        string RoleKey,
        string DisplayName,
        Guid PartyId,
        Guid? TechnicalAgentId,
        bool IsHuman);
}

internal sealed record UnitsConverterDeliveryProvisioningResult(
    Guid ProjectId,
    string ProjectName,
    Guid DefinitionId,
    Guid LaunchPlanId,
    Guid RunId,
    Guid HumanPartyId,
    string ProcessRoute,
    string ScopePhaseNodeId,
    string BuildPhaseNodeId,
    string ReleasePhaseNodeId,
    string DeliveryFeatureNodeId,
    string SolutionRelativePath,
    string WebProjectRelativePath,
    string CoreProjectRelativePath,
    string TestsProjectRelativePath,
    string BriefRelativePath,
    string BootstrapScriptRelativePath,
    string LaunchScriptRelativePath,
    string UiEvidenceRelativePath,
    IReadOnlyList<UnitsConverterRoleBindingResult> RoleBindings);

internal sealed record UnitsConverterRoleBindingResult(
    string RoleKey,
    string DisplayName,
    Guid PartyId,
    Guid? TechnicalAgentId,
    bool IsHuman);
