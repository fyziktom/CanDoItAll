using System.Text.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal sealed class AgentFrameworkIntegrationSimulationSeeder(
    ILogger<AgentFrameworkIntegrationSimulationSeeder> logger,
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    PartyDirectoryService partyDirectoryService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge)
{
    private const string ProjectName = "CanDoItAll.AgentFramework Integration Program";
    private const string ProjectMarker = "simulation:agentframework-integration";

    public async Task<AgentFrameworkSimulationSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await EnsureProjectAsync(cancellationToken);
        var parties = await EnsurePartiesAsync(cancellationToken);
        await EnsureProjectAssignmentsAsync(projectId, parties, cancellationToken);
        var graph = await EnsureProjectGraphAsync(projectId, cancellationToken);
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);

        logger.LogInformation(
            "Seeded project {ProjectId} with {PartyCount} parties and {NodeCount} nodes.",
            projectId,
            parties.Count,
            surface.Nodes.Count);

        return new AgentFrameworkSimulationSeedResult(
            projectId,
            ProjectName,
            $"/projects/{projectId:D}/structure",
            parties.Values.OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToList(),
            graph.Nodes.OrderBy(item => item.Title, StringComparer.Ordinal).ToList(),
            surface.Nodes.Count,
            surface.Links.Count);
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
                    Simulation program for integrating CanDoItAll.AgentFramework into CanDoItAll as a dedicated module while preserving role-first staffing, canonical ownership, governance, and project-graph traceability.

                    Marker: simulation:agentframework-integration
                    """,
                Objective = """
                    Build and validate the next real bundle by splitting work into role-first governance, canonical-model convergence, local-LLM-safe slices, OpenAI-assisted complex lanes, and validation/release learning loops.
                    """,
                Status = ProjectStatus.Active,
                CurrentPhase = "Role-first baseline and boundary convergence",
                TargetDateUtc = new DateTime(2026, 6, 5),
                Phases = ProjectCatalog.BuildProjectPhases().ToList(),
                Options = ProjectCatalog.BuildProjectOptions().ToList()
            },
            cancellationToken);

        return EnsureSuccess(saveResult);
    }

    private async Task<Dictionary<string, SeededParty>> EnsurePartiesAsync(CancellationToken cancellationToken)
    {
        var existing = (await partyDirectoryService.ListDirectoryAsync(cancellationToken))
            .Where(item => !string.IsNullOrWhiteSpace(item.ExternalCode))
            .ToDictionary(item => item.ExternalCode, item => item, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SeededParty> seeded = new(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in PartyCatalog.BuildPartySpecs())
        {
            existing.TryGetValue(spec.ExternalCode, out var existingParty);
            var saveResult = await partyDirectoryService.SavePartyAsync(
                new PartyEditorModel
                {
                    Id = existingParty?.Id,
                    PartyType = spec.PartyType,
                    LifecycleStatus = PartyLifecycleStatus.Active,
                    DisplayName = spec.DisplayName,
                    LegalName = spec.LegalName ?? spec.DisplayName,
                    PreferredName = spec.DisplayName,
                    ExternalCode = spec.ExternalCode,
                    Summary = spec.Summary,
                    Notes = spec.Notes,
                    Tags = [ProjectMarker, .. spec.Tags],
                    Region = "Global product delivery",
                    CountryCode = "US",
                    TimeZone = "America/La_Paz",
                    ExtendedDataJson = JsonSerializer.Serialize(
                        new
                        {
                            simulation = ProjectMarker,
                            executionLane = spec.ExecutionLane,
                            constraintSummary = spec.ConstraintSummary
                        }),
                    LastChangedBy = "scenario-seeder",
                    Roles = spec.Roles.Select(role => new PartyRoleAssignmentEditorModel
                    {
                        RoleKind = role,
                        Title = spec.DisplayName,
                        IsPrimary = true,
                        Notes = spec.ConstraintSummary
                    }).ToList(),
                    ContactPoints = spec.Email is null
                        ? []
                        : [
                            new PartyContactPointEditorModel
                            {
                                ContactType = PartyContactType.Email,
                                Label = "Primary email",
                                Value = spec.Email,
                                NormalizedValue = spec.Email.ToLowerInvariant(),
                                IsPrimary = true,
                                IsPublic = false,
                                Notes = "Simulation contact point"
                            }
                        ]
                },
                cancellationToken);

            var partyId = EnsureSuccess(saveResult);
            seeded[spec.ExternalCode] = new SeededParty(spec.ExternalCode, partyId, spec.DisplayName, spec.PartyType);
        }

        return seeded;
    }

    private async Task EnsureProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyDictionary<string, SeededParty> parties,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in PartyCatalog.BuildProjectAssignmentSpecs(projectId, parties))
        {
            EnsureSuccess(await projectPartyIntegrationBridge.SaveAssignmentAsync(assignment, cancellationToken));
        }
    }

    private async Task<SeededGraph> EnsureProjectGraphAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var rootNodeId = await GetProjectRootNodeIdAsync(projectId, cancellationToken);
        Dictionary<string, string> nodeAliases = new(StringComparer.OrdinalIgnoreCase);
        List<SeededGraphNode> seededNodes = [];

        foreach (var spec in GraphCatalog.BuildGraphSpecs())
        {
            var parentNodeId = spec.ParentAlias is null
                ? rootNodeId
                : nodeAliases[spec.ParentAlias];
            var node = await EnsureNodeAsync(projectId, parentNodeId, spec, cancellationToken);
            nodeAliases[spec.Alias] = node.Id;
            seededNodes.Add(new SeededGraphNode(spec.Alias, node.Id, node.Title, node.Route, spec.ProcessBindingName));
        }

        foreach (var link in GraphCatalog.BuildGraphLinks())
        {
            if (!nodeAliases.TryGetValue(link.SourceAlias, out var sourceNodeId) ||
                !nodeAliases.TryGetValue(link.TargetAlias, out var targetNodeId))
            {
                continue;
            }

            await projectWorkbenchService.LinkObjectsAsync(projectId, sourceNodeId, targetNodeId, link.LinkKind, cancellationToken);
        }

        return new SeededGraph(seededNodes);
    }

    private async Task<ProjectStructureNode> EnsureNodeAsync(
        Guid projectId,
        string parentNodeId,
        GraphNodeSpec spec,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var existing = surface.Nodes.FirstOrDefault(item =>
            item.ObjectType == spec.ObjectType &&
            string.Equals(item.ObjectSubtype, spec.ObjectSubtype, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Title, spec.Title, StringComparison.Ordinal) &&
            string.Equals(item.ParentId, parentNodeId, StringComparison.Ordinal));

        if (existing is null)
        {
            existing = await projectWorkbenchService.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    spec.ObjectType,
                    spec.Title,
                    spec.Subtitle,
                    ComposeNodeNotes(spec),
                    parentNodeId,
                    spec.X,
                    spec.Y,
                    spec.StartUtc,
                    spec.EndUtc,
                    spec.ObjectSubtype,
                    null,
                    "{}",
                    spec.DurationSeconds),
                cancellationToken);
        }
        else
        {
            existing = await projectWorkbenchService.UpdateObjectAsync(
                projectId,
                existing.Id,
                new ProjectObjectEditRequest(
                    spec.Title,
                    spec.Subtitle,
                    ComposeNodeNotes(spec),
                    spec.StartUtc,
                    spec.EndUtc,
                    "{}",
                    spec.DurationSeconds),
                cancellationToken)
                ?? existing;
        }

        await projectWorkbenchService.UpdateObjectMetadataAsync(
            projectId,
            existing.Id,
            "{}",
            ComposeNodeNotes(spec),
            spec.Status,
            null,
            cancellationToken);

        return (await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken)).Nodes
            .First(item => string.Equals(item.Id, existing.Id, StringComparison.Ordinal));
    }

    private async Task<string> GetProjectRootNodeIdAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        return surface.Nodes.First(item => item.ObjectType == ProjectObjectType.ProjectRoot).Id;
    }

    private static string ComposeNodeNotes(GraphNodeSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.ProcessBindingName))
        {
            return spec.Notes;
        }

        return $"{spec.Notes}\n\nDeferred workflow area: {spec.ProcessBindingName}.";
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

    private static string FormatErrors(IEnumerable<Error> errors)
    {
        return string.Join(" | ", errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    internal sealed record PartySpec(
        string ExternalCode,
        PartyType PartyType,
        string DisplayName,
        string? LegalName,
        string Summary,
        string Notes,
        string ExecutionLane,
        string ConstraintSummary,
        string? Email,
        IReadOnlyList<PartyRoleKind> Roles)
    {
        public IReadOnlyList<string> Tags { get; init; } = ["simulation", "agentframework-integration"];
    }

    internal sealed record GraphNodeSpec(
        string Alias,
        ProjectObjectType ObjectType,
        string ObjectSubtype,
        string Title,
        string Subtitle,
        string Status,
        double X,
        double Y,
        string? ParentAlias,
        string Notes,
        string? ProcessBindingName = null,
        bool BindToRun = true,
        DateTimeOffset? StartUtc = null,
        DateTimeOffset? EndUtc = null,
        int? DurationSeconds = null);

    internal sealed record GraphLinkSpec(string SourceAlias, string TargetAlias, ProjectObjectLinkKind LinkKind);
}

internal sealed record SeededParty(
    string ExternalCode,
    Guid PartyId,
    string DisplayName,
    PartyType PartyType);

internal sealed record SeededGraphNode(
    string Alias,
    string NodeId,
    string Title,
    string Route,
    string? DeferredWorkflowArea);

internal sealed record SeededGraph(IReadOnlyList<SeededGraphNode> Nodes);

internal sealed record AgentFrameworkSimulationSeedResult(
    Guid ProjectId,
    string ProjectName,
    string ProjectStructureRoute,
    IReadOnlyList<SeededParty> Parties,
    IReadOnlyList<SeededGraphNode> GraphNodes,
    int TotalGraphNodeCount,
    int TotalGraphLinkCount);
