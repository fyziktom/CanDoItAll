using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class AgentFrameworkIntegrationSimulationSeeder(
    ILogger<AgentFrameworkIntegrationSimulationSeeder> logger,
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    ProcessesService processesService,
    PartyDirectoryService partyDirectoryService,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    private const string ProjectName = "CanDoItAll.AgentFramework Integration Program";
    private const string ProjectMarker = "simulation:agentframework-integration";

    public async Task<AgentFrameworkSimulationSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var projectId = await EnsureProjectAsync(cancellationToken);
        var parties = await EnsurePartiesAsync(cancellationToken);
        await EnsureProjectAssignmentsAsync(projectId, parties, cancellationToken);
        var processes = await EnsureProcessesAsync(projectId, parties, cancellationToken);
        var graph = await EnsureProjectGraphAsync(projectId, processes, cancellationToken);
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);

        logger.LogInformation(
            "Seeded project {ProjectId} with {DefinitionCount} definitions, {RunCount} runs, and {NodeCount} nodes.",
            projectId,
            processes.Count,
            processes.Count(item => item.Value.RunId.HasValue),
            surface.Nodes.Count);

        return new AgentFrameworkSimulationSeedResult(
            projectId,
            ProjectName,
            $"/projects/{projectId:D}/processes",
            parties.Values.OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToList(),
            processes.Values.OrderBy(item => item.Name, StringComparer.Ordinal).ToList(),
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
                    Simulation program for integrating CanDoItAll.AgentFramework into CanDoItAll as a dedicated module while preserving role-first staffing, canonical ownership, process governance, and project-graph traceability.

                    Marker: simulation:agentframework-integration
                    """,
                Objective = """
                    Build and validate the next real bundle by splitting work into role-first governance, canonical-model convergence, local-LLM-safe slices, OpenAI-assisted complex lanes, and validation/release learning loops. The simulation is intentionally detailed so process and workbench gaps are visible before implementation.
                    """,
                Status = ProjectStatus.Active,
                CurrentPhase = "Role-first process baseline and boundary convergence",
                TargetDateUtc = new DateTime(2026, 6, 5),
                Phases = BuildProjectPhases().ToList(),
                Options = BuildProjectOptions().ToList()
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

        foreach (var spec in BuildPartySpecs())
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
        foreach (var assignment in BuildProjectAssignmentSpecs(projectId, parties))
        {
            EnsureSuccess(await projectPartyIntegrationBridge.SaveAssignmentAsync(assignment, cancellationToken));
        }
    }

    private async Task<Dictionary<string, SeededProcess>> EnsureProcessesAsync(
        Guid projectId,
        IReadOnlyDictionary<string, SeededParty> parties,
        CancellationToken cancellationToken)
    {
        var existingDefinitions = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, SeededProcess> seeded = new(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in BuildProcessSpecs())
        {
            existingDefinitions.TryGetValue(spec.Name, out var existingDefinition);

            var roleIds = spec.Roles.ToDictionary(
                item => item.Key,
                item => CreateStableGuid($"{spec.Name}:role:{item.Key}"),
                StringComparer.OrdinalIgnoreCase);
            var stepIds = spec.Steps.ToDictionary(
                item => item.Key,
                item => CreateStableGuid($"{spec.Name}:step:{item.Key}"),
                StringComparer.OrdinalIgnoreCase);

            var definitionId = EnsureSuccess(await processesService.SaveAsync(
                BuildProcessEditorModel(spec, projectId, existingDefinition?.Id, roleIds, stepIds),
                cancellationToken));

            var publishResult = await processesService.PublishAsync(definitionId, cancellationToken);
            if (publishResult.IsFailure &&
                !publishResult.Errors.Any(error => string.Equals(error.Code, "processes.no-draft-version", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(FormatErrors(publishResult.Errors));
            }

            var editor = await processesService.GetEditorAsync(definitionId, projectId, cancellationToken);
            var currentRoleIds = editor.Roles.ToDictionary(item => item.Key, item => item.Id ?? Guid.Empty, StringComparer.OrdinalIgnoreCase);
            var currentStepIds = editor.Steps.ToDictionary(item => item.Key, item => item.Id ?? Guid.Empty, StringComparer.OrdinalIgnoreCase);
            var existingRun = (await processesService.ListRunsAsync(definitionId, projectId, cancellationToken))
                .FirstOrDefault(item => string.Equals(item.Name, spec.RunName, StringComparison.OrdinalIgnoreCase));

            Guid? runId = existingRun?.Id;
            if (!runId.HasValue)
            {
                runId = EnsureSuccess(await processesService.StartRunAsync(
                    new ProcessRunStartRequest
                    {
                        ProcessDefinitionId = definitionId,
                        ProjectId = projectId,
                        RunName = spec.RunName,
                        OperatingMode = spec.OperatingMode,
                        TriggerReason = $"Scenario seeding for {ProjectMarker}"
                    },
                    cancellationToken));
            }

            if (runId.HasValue)
            {
                await EnsureRunAssignmentsAsync(runId.Value, spec, currentRoleIds, parties, cancellationToken);
                await EnsureRunRuntimeStateAsync(runId.Value, spec, currentStepIds, cancellationToken);
            }

            seeded[spec.Name] = new SeededProcess(
                definitionId,
                projectId,
                spec.Name,
                spec.RunName,
                runId,
                spec.OperatingMode.ToString(),
                spec.Summary,
                $"/projects/{projectId:D}/processes?processId={definitionId:D}{(runId.HasValue ? $"&runId={runId.Value:D}" : string.Empty)}");
        }

        return seeded;
    }

    private async Task EnsureRunAssignmentsAsync(
        Guid runId,
        ProcessSpec spec,
        IReadOnlyDictionary<string, Guid> roleIds,
        IReadOnlyDictionary<string, SeededParty> parties,
        CancellationToken cancellationToken)
    {
        var existingAssignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        foreach (var role in spec.Roles)
        {
            if (!roleIds.TryGetValue(role.Key, out var roleId) ||
                roleId == Guid.Empty ||
                !parties.TryGetValue(role.AssignedPartyExternalCode, out var party))
            {
                continue;
            }

            var existing = existingAssignments.FirstOrDefault(item =>
                item.RoleRequirementId == roleId &&
                item.StepDefinitionId is null);
            if (existing is not null &&
                existing.PartyId == party.PartyId &&
                string.Equals(existing.DisplayName, party.DisplayName, StringComparison.Ordinal))
            {
                continue;
            }

            EnsureSuccess(await processesService.ResolveAssignmentAsync(
                new ProcessAssignmentResolutionRequest
                {
                    ProcessRunId = runId,
                    RoleRequirementId = roleId,
                    PartyId = party.PartyId,
                    DisplayName = party.DisplayName,
                    ExecutorKind = role.ExecutorKind,
                    BindingReason = role.BindingReason,
                    IsFallback = false
                },
                cancellationToken));
        }
    }

    private async Task EnsureRunRuntimeStateAsync(
        Guid runId,
        ProcessSpec spec,
        IReadOnlyDictionary<string, Guid> stepIds,
        CancellationToken cancellationToken)
    {
        foreach (var transition in spec.StepTransitions)
        {
            EnsureSuccess(await EnsureStepStatusAsync(
                runId,
                transition.Sequence,
                transition.TargetStatus,
                transition.Reason,
                transition.DecidedBy,
                cancellationToken));
        }

        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);
        var refreshedStepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        foreach (var artifact in spec.Artifacts)
        {
            if (!stepIds.TryGetValue(artifact.StepKey, out var stepDefinitionId))
            {
                continue;
            }

            var stepRun = refreshedStepRuns.FirstOrDefault(item => item.StepDefinitionId == stepDefinitionId);
            if (stepRun is null ||
                artifacts.Any(item => string.Equals(item.Title, artifact.Title, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            EnsureSuccess(await processesService.RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = runId,
                    StepRunId = stepRun.Id,
                    ArtifactKind = artifact.ArtifactKind,
                    Title = artifact.Title,
                    TrustStatus = artifact.TrustStatus,
                    SensitivityLevel = artifact.SensitivityLevel,
                    ProvenanceSummary = artifact.ProvenanceSummary,
                    AllowedFutureUsageSummary = artifact.AllowedFutureUsageSummary,
                    ReviewSummary = artifact.ReviewSummary
                },
                cancellationToken));
        }
    }

    private async Task<SeededGraph> EnsureProjectGraphAsync(
        Guid projectId,
        IReadOnlyDictionary<string, SeededProcess> processes,
        CancellationToken cancellationToken)
    {
        var rootNodeId = await GetProjectRootNodeIdAsync(projectId, cancellationToken);
        Dictionary<string, string> nodeAliases = new(StringComparer.OrdinalIgnoreCase);
        List<SeededGraphNode> seededNodes = [];

        foreach (var spec in BuildGraphSpecs())
        {
            var parentNodeId = spec.ParentAlias is null
                ? rootNodeId
                : nodeAliases[spec.ParentAlias];
            var node = await EnsureNodeAsync(projectId, parentNodeId, spec, cancellationToken);
            nodeAliases[spec.Alias] = node.Id;
            seededNodes.Add(new SeededGraphNode(spec.Alias, node.Id, node.Title, node.Route, spec.ProcessBindingName));

            if (!string.IsNullOrWhiteSpace(spec.ProcessBindingName) &&
                processes.TryGetValue(spec.ProcessBindingName, out var boundProcess))
            {
                await UpsertProcessBindingAsync(projectId, node.Id, boundProcess, spec.BindToRun, cancellationToken);
            }
        }

        foreach (var link in BuildGraphLinks())
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

    private async Task UpsertProcessBindingAsync(
        Guid projectId,
        string nodeId,
        SeededProcess process,
        bool bindToRun,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var node = await dbContext.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.ProjectId == projectId && item.NodeKey == nodeId, cancellationToken);
        var binding = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleOrDefaultAsync(item => item.ProjectObjectId == node.Id, cancellationToken);
        if (binding is null)
        {
            binding = new ProjectNodeBindingRecord
            {
                ProjectObjectId = node.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            await dbContext.Set<ProjectNodeBindingRecord>().AddAsync(binding, cancellationToken);
        }

        binding.Route = bindToRun && process.RunId.HasValue
            ? $"/projects/{projectId:D}/processes?processId={process.DefinitionId:D}&runId={process.RunId.Value:D}"
            : $"/projects/{projectId:D}/processes?processId={process.DefinitionId:D}";
        binding.ExternalArtifactKind = bindToRun && process.RunId.HasValue
            ? "process-run"
            : "process-definition";
        binding.ExternalArtifactId = bindToRun && process.RunId.HasValue
            ? process.RunId.Value
            : process.DefinitionId;
        binding.MediaRelativePath = string.Empty;
        binding.MediaContentType = string.Empty;
        binding.MediaOriginalFileName = string.Empty;
        binding.StorageObjectReferenceJson = string.Empty;
        binding.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
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

        return $"{spec.Notes}\n\nAssigned process workspace: {spec.ProcessBindingName}.";
    }

    private static CanDoItAll.Modules.Processes.ProcessDefinitionEditorModel BuildProcessEditorModel(
        ProcessSpec spec,
        Guid projectId,
        Guid? definitionId,
        IReadOnlyDictionary<string, Guid> roleIds,
        IReadOnlyDictionary<string, Guid> stepIds)
    {
        return new CanDoItAll.Modules.Processes.ProcessDefinitionEditorModel
        {
            Id = definitionId,
            ProjectId = projectId,
            Name = spec.Name,
            Summary = spec.Summary,
            ValueStatement = spec.ValueStatement,
            CustomerName = "CanDoItAll Product Steering Committee",
            OwnerName = "Platform Delivery Guild",
            InterfaceContractSummary = spec.InterfaceContractSummary,
            GovernanceNotes = spec.GovernanceNotes,
            ChangeSummary = "Refreshed scenario seed for AgentFramework integration simulation.",
            GovernancePolicySummary = spec.GovernancePolicySummary,
            ConstitutionRuleSummary = spec.ConstitutionRuleSummary,
            OperatingModeSummary = spec.OperatingModeSummary,
            SimulationReadinessSummary = spec.SimulationReadinessSummary,
            Criticality = spec.Criticality,
            AutonomyLevel = spec.AutonomyLevel,
            Roles = spec.Roles.Select(role => new CanDoItAll.Modules.Processes.ProcessRoleEditorModel
            {
                Id = roleIds[role.Key],
                Key = role.Key,
                DisplayName = role.DisplayName,
                Purpose = role.Purpose,
                StaffingIntent = role.StaffingIntent,
                PreferredExecutorKind = role.ExecutorKind,
                PreferredProjectAssignmentRole = role.PreferredProjectAssignmentRole,
                IsRequired = true,
                AllowsFallback = role.AllowsFallback,
                RequiresExplicitApproval = role.RequiresExplicitApproval,
                DefaultAllocationPercent = role.DefaultAllocationPercent,
                RoleTemplateSourceKey = $"{ProjectMarker}:{role.Key}",
                RoleTemplateSnapshotName = $"{spec.Name} / {role.DisplayName}",
                SnapshotSummary = role.SnapshotSummary
            }).ToList(),
            Steps = spec.Steps.Select(step => new CanDoItAll.Modules.Processes.ProcessStepEditorModel
            {
                Id = stepIds[step.Key],
                Key = step.Key,
                Title = step.Title,
                Subtitle = step.Subtitle,
                Notes = step.Notes,
                StepKind = step.StepKind,
                AllowsManualSkip = false,
                AllowsSafeRefusal = step.AllowsSafeRefusal,
                RequiresApproval = step.RequiresApproval,
                RequiresDecisionRecord = step.RequiresDecisionRecord,
                InputContractSummary = step.InputContractSummary,
                OutputContractSummary = step.OutputContractSummary,
                EvidenceContractSummary = step.EvidenceContractSummary,
                DecisionRightsSummary = step.DecisionRightsSummary,
                ExceptionPolicySummary = step.ExceptionPolicySummary,
                TargetLeadHours = step.TargetLeadHours,
                Dependencies = string.IsNullOrWhiteSpace(step.DependsOnStepKey)
                    ? []
                    : [
                        new CanDoItAll.Modules.Processes.ProcessStepDependencyEditorModel
                        {
                            Id = CreateStableGuid($"{spec.Name}:step:{step.Key}:dependency:primary"),
                            DependsOnStepId = stepIds[step.DependsOnStepKey]
                        }
                    ],
                CanvasX = step.CanvasX,
                CanvasY = step.CanvasY,
                RoleAssignments = step.Assignments.Select(assignment => new CanDoItAll.Modules.Processes.ProcessStepRoleRequirementEditorModel
                {
                    RoleRequirementId = roleIds[assignment.RoleKey],
                    ResponsibilityKind = assignment.ResponsibilityKind,
                    IsRequired = true,
                    FallbackOrder = assignment.FallbackOrder,
                    RebindPolicySummary = assignment.RebindPolicySummary
                }).ToList(),
                ArtifactExpectations = step.ArtifactExpectations.Select(artifact => new CanDoItAll.Modules.Processes.ProcessArtifactExpectationEditorModel
                {
                    ArtifactKind = artifact.ArtifactKind,
                    Title = artifact.Title,
                    IsRequired = true,
                    TrustRequirement = artifact.TrustRequirement,
                    SensitivityLevel = artifact.SensitivityLevel,
                    RetentionDays = artifact.RetentionDays,
                    AllowedFutureUsageSummary = artifact.AllowedFutureUsageSummary,
                    ValidationRequirementSummary = artifact.ValidationRequirementSummary
                }).ToList()
            }).ToList()
        };
    }

    private async Task<Result> EnsureStepStatusAsync(
        Guid runId,
        int sequence,
        ProcessStepRunStatus targetStatus,
        string reason,
        string decidedBy,
        CancellationToken cancellationToken)
    {
        var stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
            .FirstOrDefault(item => item.Sequence == sequence);
        if (stepRun is null || stepRun.Status == targetStatus)
        {
            return Result.Success();
        }

        if (targetStatus == ProcessStepRunStatus.Completed &&
            stepRun.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.Blocked)
        {
            var startResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRun.Id,
                    TargetStatus = ProcessStepRunStatus.InProgress,
                    Reason = $"Scenario progression for {reason}",
                    DecidedBy = decidedBy
                },
                cancellationToken);
            if (startResult.IsFailure)
            {
                return startResult;
            }
        }

        stepRun = (await processesService.ListStepRunsAsync(runId, cancellationToken))
            .FirstOrDefault(item => item.Sequence == sequence);
        if (stepRun is null || stepRun.Status == targetStatus)
        {
            return Result.Success();
        }

        if (stepRun.Status is ProcessStepRunStatus.Ready or
            ProcessStepRunStatus.WaitingApproval or
            ProcessStepRunStatus.InProgress or
            ProcessStepRunStatus.Blocked)
        {
            return await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRun.Id,
                    TargetStatus = targetStatus,
                    Reason = reason,
                    DecidedBy = decidedBy
                },
                cancellationToken);
        }

        return Result.Success();
    }

    private static Guid CreateStableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
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

    private static IReadOnlyList<ProjectPhaseEditorModel> BuildProjectPhases() => ProjectCatalog.BuildProjectPhases();

    private static IReadOnlyList<ProjectOptionEditorModel> BuildProjectOptions() => ProjectCatalog.BuildProjectOptions();

    private static IReadOnlyList<PartySpec> BuildPartySpecs() => PartyCatalog.BuildPartySpecs();

    private static IReadOnlyList<ProjectPartyAssignmentUpsertRequest> BuildProjectAssignmentSpecs(
        Guid projectId,
        IReadOnlyDictionary<string, SeededParty> parties) => PartyCatalog.BuildProjectAssignmentSpecs(projectId, parties);

    private static IReadOnlyList<ProcessSpec> BuildProcessSpecs() => ProcessCatalog.BuildProcessSpecs();

    private static IReadOnlyList<GraphNodeSpec> BuildGraphSpecs() => GraphCatalog.BuildGraphSpecs();

    private static IReadOnlyList<GraphLinkSpec> BuildGraphLinks() => GraphCatalog.BuildGraphLinks();

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

    internal sealed record ProcessSpec(
        string Name,
        string RunName,
        ProcessOperatingMode OperatingMode,
        ProcessCriticality Criticality,
        ProcessAutonomyLevel AutonomyLevel,
        string Summary,
        string ValueStatement,
        string InterfaceContractSummary,
        string GovernanceNotes,
        string ConstitutionRuleSummary,
        string OperatingModeSummary,
        string SimulationReadinessSummary,
        IReadOnlyList<ProcessRoleSpec> Roles,
        IReadOnlyList<ProcessStepSpec> Steps,
        IReadOnlyList<StepTransitionSpec> StepTransitions,
        IReadOnlyList<ArtifactSpec> Artifacts)
    {
        public string GovernancePolicySummary => GovernanceNotes;
    }

    internal sealed record ProcessRoleSpec(
        string Key,
        string DisplayName,
        string Purpose,
        string StaffingIntent,
        string ExecutorKind,
        ProjectPartyAssignmentRole PreferredProjectAssignmentRole,
        bool RequiresExplicitApproval,
        bool AllowsFallback,
        int DefaultAllocationPercent,
        string SnapshotSummary,
        string AssignedPartyExternalCode,
        string BindingReason);

    internal sealed record ProcessStepSpec(
        string Key,
        string Title,
        string Subtitle,
        string Notes,
        ProcessStepKind StepKind,
        bool AllowsSafeRefusal,
        bool RequiresApproval,
        bool RequiresDecisionRecord,
        string InputContractSummary,
        string OutputContractSummary,
        string EvidenceContractSummary,
        string DecisionRightsSummary,
        string ExceptionPolicySummary,
        int TargetLeadHours,
        double CanvasX,
        double CanvasY,
        string? DependsOnStepKey,
        IReadOnlyList<StepRoleAssignmentSpec> Assignments,
        IReadOnlyList<StepArtifactSpec> ArtifactExpectations);

    internal sealed record StepRoleAssignmentSpec(
        string RoleKey,
        ProcessResponsibilityKind ResponsibilityKind,
        int FallbackOrder,
        string RebindPolicySummary);

    internal sealed record StepArtifactSpec(
        ProcessArtifactKind ArtifactKind,
        string Title,
        ProcessArtifactTrustRequirement TrustRequirement,
        ProcessSensitivityLevel SensitivityLevel,
        int RetentionDays,
        string AllowedFutureUsageSummary,
        string ValidationRequirementSummary);

    internal sealed record StepTransitionSpec(
        int Sequence,
        ProcessStepRunStatus TargetStatus,
        string Reason,
        string DecidedBy);

    internal sealed record ArtifactSpec(
        string StepKey,
        ProcessArtifactKind ArtifactKind,
        string Title,
        ProcessArtifactTrustStatus TrustStatus,
        ProcessSensitivityLevel SensitivityLevel,
        string ProvenanceSummary,
        string AllowedFutureUsageSummary,
        string ReviewSummary);

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

internal sealed record SeededProcess(
    Guid DefinitionId,
    Guid ProjectId,
    string Name,
    string RunName,
    Guid? RunId,
    string OperatingMode,
    string Summary,
    string Route);

internal sealed record SeededGraphNode(
    string Alias,
    string NodeId,
    string Title,
    string Route,
    string? BoundProcessName);

internal sealed record SeededGraph(IReadOnlyList<SeededGraphNode> Nodes);

internal sealed record AgentFrameworkSimulationSeedResult(
    Guid ProjectId,
    string ProjectName,
    string ProjectProcessesRoute,
    IReadOnlyList<SeededParty> Parties,
    IReadOnlyList<SeededProcess> Processes,
    IReadOnlyList<SeededGraphNode> GraphNodes,
    int TotalGraphNodeCount,
    int TotalGraphLinkCount);
