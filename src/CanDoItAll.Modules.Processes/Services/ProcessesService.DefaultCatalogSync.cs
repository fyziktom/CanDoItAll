using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    internal async Task<Result<bool>> SynchronizeImportedDefinitionAsync(
        Guid definitionId,
        ProcessImportExportEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (definitionId == Guid.Empty)
        {
            return Result<bool>.Failure(
                Error.Validation("Process definition was not found.", "processes.definition-not-found"));
        }

        ArgumentNullException.ThrowIfNull(envelope);

        var current = await GetEditorAsync(definitionId, null, cancellationToken);
        if (!current.Id.HasValue)
        {
            return Result<bool>.Failure(
                Error.Validation("Process definition was not found.", "processes.definition-not-found"));
        }

        var incoming = ProcessDependencyCompatibilityBridge.ToEditorModel(envelope.Definition);
        incoming.Id = current.Id;
        incoming.ProjectId = current.ProjectId;
        incoming.WorkingVersionId = current.WorkingVersionId;
        incoming.DefinitionConcurrencyToken = current.DefinitionConcurrencyToken;
        incoming.WorkingVersionConcurrencyToken = current.WorkingVersionConcurrencyToken;
        incoming.WorkingVersionNumber = current.WorkingVersionNumber;
        incoming.Status = current.Status;
        var subprocessResolution = await ResolveImportedSubprocessReferencesAsync(incoming, cancellationToken);
        if (subprocessResolution.IsFailure)
        {
            return Result<bool>.Failure(subprocessResolution.Errors);
        }

        if (ProcessDefinitionSyncComparer.AreEquivalent(current, incoming))
        {
            return Result<bool>.Success(false);
        }

        PrepareImportedDefinitionForSave(incoming, resetDefinitionIdentity: false);

        var saveResult = await SaveAsync(
            incoming,
            new ProcessImportMetadata(
                envelope.SourceFormat,
                string.Join(Environment.NewLine, envelope.Warnings)),
            cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<bool>.Failure(saveResult.Errors);
        }

        return Result<bool>.Success(true);
    }
}

internal static class ProcessDefinitionSyncComparer
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web);

    public static bool AreEquivalent(
        ProcessDefinitionEditorModel current,
        ProcessDefinitionEditorModel incoming)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(incoming);

        return string.Equals(
            CreateFingerprint(current),
            CreateFingerprint(incoming),
            StringComparison.Ordinal);
    }

    private static string CreateFingerprint(ProcessDefinitionEditorModel model)
    {
        var roleKeysById = model.Roles
            .Where(item => item.Id.HasValue)
            .ToDictionary(item => item.Id!.Value, item => item.Key);
        var branchOutcomesById = new Dictionary<Guid, (string StepKey, string BranchKey)>();
        var artifactExpectationsById = new Dictionary<Guid, (string StepKey, ProcessArtifactKind ArtifactKind, string Title)>();

        foreach (var step in model.Steps)
        {
            foreach (var branchOutcome in step.BranchOutcomes.Where(item => item.Id.HasValue))
            {
                branchOutcomesById[branchOutcome.Id!.Value] = (step.Key, branchOutcome.Key);
            }

            foreach (var artifactExpectation in step.ArtifactExpectations.Where(item => item.Id.HasValue))
            {
                artifactExpectationsById[artifactExpectation.Id!.Value] = (step.Key, artifactExpectation.ArtifactKind, artifactExpectation.Title);
            }
        }

        var fingerprint = new
        {
            model.Name,
            model.Summary,
            model.ValueStatement,
            model.CustomerName,
            model.OwnerName,
            model.InterfaceContractSummary,
            model.GovernanceNotes,
            model.ChangeSummary,
            model.GovernancePolicySummary,
            model.ConstitutionRuleSummary,
            model.OperatingModeSummary,
            model.SimulationReadinessSummary,
            ManagerAgentOverrideId = model.ManagerAgentOverrideId?.ToString("D") ?? string.Empty,
            model.ManagerAgentOverrideName,
            model.Criticality,
            model.AutonomyLevel,
            model.ContractMode,
            Roles = model.Roles
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new
                {
                    item.Key,
                    item.DisplayName,
                    item.Purpose,
                    item.StaffingIntent,
                    item.PreferredExecutorKind,
                    item.PreferredProjectAssignmentRole,
                    item.IsRequired,
                    item.AllowsFallback,
                    item.RequiresExplicitApproval,
                    item.DefaultAllocationPercent,
                    item.RoleTemplateSourceKey,
                    item.RoleTemplateSnapshotName,
                    item.SnapshotSummary,
                    RequiredSkillIds = item.RequiredSkillIds
                        .Select(skillId => skillId.ToString("D"))
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    item.CanvasX,
                    item.CanvasY
                })
                .ToArray(),
            MessagingPolicies = model.MessagingPolicies
                .Select(item => new
                {
                    SourceRoleKey = item.SourceRoleRequirementId.HasValue &&
                                    roleKeysById.TryGetValue(item.SourceRoleRequirementId.Value, out var sourceRoleKey)
                        ? sourceRoleKey
                        : string.Empty,
                    TargetRoleKey = item.TargetRoleRequirementId.HasValue &&
                                    roleKeysById.TryGetValue(item.TargetRoleRequirementId.Value, out var targetRoleKey)
                        ? targetRoleKey
                        : string.Empty
                })
                .OrderBy(item => item.SourceRoleKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.TargetRoleKey, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Steps = model.Steps
                .Select(step => new
                {
                    step.Key,
                    step.Title,
                    step.Subtitle,
                    step.Notes,
                    step.StepKind,
                    SubprocessDefinitionId = step.SubprocessDefinitionId?.ToString("D") ?? string.Empty,
                    step.SubprocessDefinitionSnapshotName,
                    step.AllowsManualSkip,
                    step.AllowsSafeRefusal,
                    step.RequiresApproval,
                    step.RequiresDecisionRecord,
                    step.InputContractSummary,
                    step.OutputContractSummary,
                    step.EvidenceContractSummary,
                    step.DecisionRightsSummary,
                    step.ExceptionPolicySummary,
                    step.TargetLeadHours,
                    DecisionRoleKey = step.DecisionRoleRequirementId.HasValue &&
                                      roleKeysById.TryGetValue(step.DecisionRoleRequirementId.Value, out var decisionRoleKey)
                        ? decisionRoleKey
                        : string.Empty,
                    step.CanvasX,
                    step.CanvasY,
                    step.BranchCanvasX,
                    step.BranchCanvasY,
                    BranchOutcomes = step.BranchOutcomes
                        .Select(item => new
                        {
                            item.Key,
                            item.Title,
                            item.Description
                        })
                        .ToArray(),
                    Dependencies = step.Dependencies
                        .Select(item => new
                        {
                            DependsOnStepKey = ResolveStepKey(model, item.DependsOnStepId),
                            DependsOnBranchOutcomeKey = item.DependsOnBranchOutcomeId.HasValue &&
                                                        branchOutcomesById.TryGetValue(item.DependsOnBranchOutcomeId.Value, out var dependencyBranch)
                                ? dependencyBranch.BranchKey
                                : string.Empty
                        })
                        .OrderBy(item => item.DependsOnStepKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.DependsOnBranchOutcomeKey, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    RoleAssignments = step.RoleAssignments
                        .Select(item => new
                        {
                            RoleKey = item.RoleRequirementId.HasValue &&
                                      roleKeysById.TryGetValue(item.RoleRequirementId.Value, out var assignmentRoleKey)
                                ? assignmentRoleKey
                                : string.Empty,
                            item.ResponsibilityKind,
                            item.IsRequired,
                            item.FallbackOrder,
                            item.RebindPolicySummary
                        })
                        .OrderBy(item => item.RoleKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.ResponsibilityKind)
                        .ThenBy(item => item.FallbackOrder)
                        .ToArray(),
                    ArtifactExpectations = step.ArtifactExpectations
                        .Select(item => new
                        {
                            item.ArtifactKind,
                            item.Title,
                            item.IsRequired,
                            item.TrustRequirement,
                            item.SensitivityLevel,
                            item.RetentionDays,
                            item.AllowedFutureUsageSummary,
                            item.ValidationRequirementSummary,
                            item.WorkflowOutputId,
                            item.WorkflowOutputName,
                            item.WorkflowOutputKind,
                            item.SubprocessChildArtifactExpectationId
                        })
                        .ToArray(),
                    ArtifactInputs = step.ArtifactInputs
                        .Select(item =>
                        {
                            if (item.ArtifactExpectationId.HasValue &&
                                artifactExpectationsById.TryGetValue(item.ArtifactExpectationId.Value, out var artifactInput))
                            {
                                return new
                                {
                                    artifactInput.StepKey,
                                    artifactInput.ArtifactKind,
                                    artifactInput.Title
                                };
                            }

                            return new
                            {
                                StepKey = string.Empty,
                                ArtifactKind = ProcessArtifactKind.Evidence,
                                Title = string.Empty
                            };
                        })
                        .OrderBy(item => item.StepKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.ArtifactKind)
                        .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                })
                .ToArray()
        };

        return JsonSerializer.Serialize(fingerprint, CanonicalJsonOptions);
    }

    private static string ResolveStepKey(
        ProcessDefinitionEditorModel model,
        Guid? stepId)
    {
        if (!stepId.HasValue)
        {
            return string.Empty;
        }

        return model.Steps
            .FirstOrDefault(item => item.Id == stepId.Value)
            ?.Key ?? string.Empty;
    }
}
