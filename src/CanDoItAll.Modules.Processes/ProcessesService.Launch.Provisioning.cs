using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result> ProvisionLaunchPlanAsync(
        Guid launchPlanId,
        string requestedBy = "process-workspace",
        CancellationToken cancellationToken = default)
    {
        if (launchPlanId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Launch plan is required.", "processes.launch.plan-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var plan = await dbContext.Set<ProcessLaunchPlan>()
                .SingleOrDefaultAsync(item => item.Id == launchPlanId, cancellationToken);
            if (plan is null)
            {
                return Result.Failure(Error.Validation("Launch plan was not found.", "processes.launch.not-found"));
            }

            if (plan.Status is not ProcessLaunchPlanStatus.Provisioning and not ProcessLaunchPlanStatus.Ready)
            {
                return Result.Failure(Error.Validation(
                    "Launch plan is not in provisioning state.",
                    "processes.launch.provisioning-state-invalid"));
            }

            var roles = await dbContext.Set<ProcessLaunchPlanRole>()
                .Where(item => item.LaunchPlanId == plan.Id)
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
            var selectedCandidateIds = roles
                .Where(item => item.SelectedCandidateId.HasValue)
                .Select(item => item.SelectedCandidateId!.Value)
                .Distinct()
                .ToList();
            var candidates = selectedCandidateIds.Count == 0
                ? []
                : await dbContext.Set<ProcessLaunchCandidate>()
                    .Where(item => selectedCandidateIds.Contains(item.Id))
                    .ToListAsync(cancellationToken);
            var candidateLookup = candidates.ToDictionary(item => item.Id);

            if (!plan.ProjectId.HasValue &&
                roles.Any(item => item.RequiresProvisioning))
            {
                return Result.Failure(Error.Validation(
                    "Provisioning requires a project-scoped launch plan so new resources can be attached to the project.",
                    "processes.launch.project-required-for-provisioning"));
            }

            var skillNames = await LoadSkillNamesAsync(dbContext, roles, cancellationToken);
            var now = clock.GetUtcNow();
            foreach (var role in roles.Where(item => item.RequiresProvisioning))
            {
                if (!role.SelectedCandidateId.HasValue ||
                    !candidateLookup.TryGetValue(role.SelectedCandidateId.Value, out var selectedCandidate))
                {
                    return Result.Failure(Error.Validation(
                        $"Provisioning role '{role.DisplayName}' no longer has a selected candidate.",
                        "processes.launch.provisioning-candidate-missing"));
                }

                var provisioningRequest = await dbContext.Set<ProcessLaunchProvisioningRequest>()
                    .SingleOrDefaultAsync(
                        item => item.LaunchPlanId == plan.Id && item.LaunchPlanRoleId == role.Id,
                        cancellationToken);
                if (provisioningRequest is null)
                {
                    provisioningRequest = new ProcessLaunchProvisioningRequest
                    {
                        LaunchPlanId = plan.Id,
                        LaunchPlanRoleId = role.Id,
                        SelectedCandidateId = selectedCandidate.Id,
                        Status = ProcessLaunchProvisioningStatus.Pending,
                        RequestKind = ResolveProvisioningRequestKind(role, selectedCandidate),
                        Title = $"Provision {role.DisplayName}",
                        RequestPayloadJson = selectedCandidate.MetadataJson,
                        CreatedAtUtc = now
                    };
                    await dbContext.Set<ProcessLaunchProvisioningRequest>().AddAsync(provisioningRequest, cancellationToken);
                }
                else
                {
                    provisioningRequest.SelectedCandidateId = selectedCandidate.Id;
                    provisioningRequest.Status = ProcessLaunchProvisioningStatus.Pending;
                    provisioningRequest.RequestKind = ResolveProvisioningRequestKind(role, selectedCandidate);
                    provisioningRequest.Title = $"Provision {role.DisplayName}";
                    provisioningRequest.RequestPayloadJson = selectedCandidate.MetadataJson;
                }

                var provisioningOutcome = await ProvisionSelectedLaunchCandidateAsync(
                    plan,
                    role,
                    selectedCandidate,
                    skillNames,
                    requestedBy,
                    cancellationToken);
                if (provisioningOutcome.IsFailure)
                {
                    provisioningRequest.Status = ProcessLaunchProvisioningStatus.Rejected;
                    provisioningRequest.ResultSummary = string.Join(" ", provisioningOutcome.Errors.Select(error => error.Message));
                    provisioningRequest.CompletedAtUtc = clock.GetUtcNow();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(provisioningOutcome.Errors);
                }

                var resolvedProvisioning = provisioningOutcome.Value!;
                provisioningRequest.Status = ProcessLaunchProvisioningStatus.Provisioned;
                provisioningRequest.ResultPartyId = resolvedProvisioning.PartyId;
                provisioningRequest.ResultTechnicalAgentId = resolvedProvisioning.TechnicalAgentId;
                provisioningRequest.ResultSummary = resolvedProvisioning.Summary;
                provisioningRequest.CompletedAtUtc = clock.GetUtcNow();

                selectedCandidate.PartyId = resolvedProvisioning.PartyId;
                selectedCandidate.TechnicalAgentId = resolvedProvisioning.TechnicalAgentId;
                selectedCandidate.RequiresProvisioning = false;
                selectedCandidate.AvailabilitySummary = resolvedProvisioning.Summary;
                if (string.IsNullOrWhiteSpace(selectedCandidate.RecommendationSummary))
                {
                    selectedCandidate.RecommendationSummary = resolvedProvisioning.Summary;
                }

                role.RequiresProvisioning = false;
                role.IsResolved = true;
                role.SelectionSummary = ResolveLaunchSelectionSummary(selectedCandidate);
                role.ReadinessSummary = "Provisioned and ready for execution.";
            }

            plan.Status = roles.Any(item => item.RequiresProvisioning)
                ? ProcessLaunchPlanStatus.Provisioning
                : ProcessLaunchPlanStatus.Ready;
            plan.UpdatedAtUtc = clock.GetUtcNow();

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(
                "Launch provisioning conflicted with another update. Reload and try again.",
                "processes.launch.provisioning-conflict"));
        }
    }

    private async Task<Result<LaunchProvisioningOutcome>> ProvisionSelectedLaunchCandidateAsync(
        ProcessLaunchPlan plan,
        ProcessLaunchPlanRole role,
        ProcessLaunchCandidate selectedCandidate,
        IReadOnlyDictionary<Guid, string> skillNames,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        if (!plan.ProjectId.HasValue)
        {
            return Result<LaunchProvisioningOutcome>.Failure(Error.Validation(
                "Provisioning requires a project-scoped launch plan.",
                "processes.launch.project-required-for-provisioning"));
        }

        if (selectedCandidate.CandidateKind == ProcessLaunchCandidateKind.Gap)
        {
            return Result<LaunchProvisioningOutcome>.Failure(Error.Validation(
                $"Role '{role.DisplayName}' is unresolved and cannot be provisioned.",
                "processes.launch.provisioning-gap"));
        }

        if (!IsAiRoleFromLaunchRole(role) &&
            selectedCandidate.CandidateKind != ProcessLaunchCandidateKind.NewAiAgentProposal)
        {
            return Result<LaunchProvisioningOutcome>.Success(
                new LaunchProvisioningOutcome(
                    selectedCandidate.PartyId,
                    selectedCandidate.TechnicalAgentId,
                    "No provisioning was required for this candidate."));
        }

        var metadata = ParseLaunchProvisioningMetadata(selectedCandidate.MetadataJson);
        var partyId = selectedCandidate.PartyId ?? metadata.ExistingPartyId;
        if (!partyId.HasValue || partyId.Value == Guid.Empty)
        {
            var createPartyResult = await projectPartyIntegrationBridge.CreatePartyAsync(
                new ProjectPartyQuickCreateRequest
                {
                    ProjectId = plan.ProjectId.Value,
                    PartyKind = ProjectPartyQuickCreateKind.AiAgent,
                    DisplayName = string.IsNullOrWhiteSpace(metadata.DisplayName)
                        ? selectedCandidate.DisplayName
                        : metadata.DisplayName,
                    Summary = selectedCandidate.RecommendationSummary
                },
                cancellationToken);
            if (createPartyResult.IsFailure)
            {
                return Result<LaunchProvisioningOutcome>.Failure(createPartyResult.Errors);
            }

            partyId = createPartyResult.Value.PartyId;
        }

        var technicalWorkspace = await aiAgentService.GetAgentWorkspaceAsync(partyId.Value, cancellationToken);
        if (technicalWorkspace is null)
        {
            return Result<LaunchProvisioningOutcome>.Failure(Error.Validation(
                "Provisioning candidate is not a valid AI resource party.",
                "processes.launch.provisioning-party-invalid"));
        }

        var capabilityMetadata = metadata.RequiredSkillIds
            .Where(skillNames.ContainsKey)
            .Select(skillId => new AiCapabilityEditorModel
            {
                Name = skillNames[skillId],
                Scope = $"Required skill for process role '{role.DisplayName}'.",
                ToolAccess = string.Empty,
                Limitations = string.Empty,
                Notes = selectedCandidate.RecommendationSummary
            })
            .ToList();
        if (capabilityMetadata.Count == 0 && technicalWorkspace.Profile.Capabilities.Count == 0)
        {
            capabilityMetadata.Add(new AiCapabilityEditorModel
            {
                Name = role.DisplayName,
                Scope = $"Provisioned for launch plan '{plan.Name}'.",
                ToolAccess = string.Empty,
                Limitations = string.Empty,
                Notes = selectedCandidate.RecommendationSummary
            });
        }

        var saveProfileResult = await aiAgentService.SaveAgentProfileAsync(
            new AiAgentProfileEditorModel
            {
                PartyId = partyId.Value,
                ProviderProfileId = technicalWorkspace.Profile.ProviderProfileId,
                DefaultModel = technicalWorkspace.Profile.DefaultModel,
                ExecutionMode = technicalWorkspace.Profile.ExecutionMode,
                OwnerPartyId = null,
                ValidationStatus = AiValidationStatus.Draft,
                LastReviewedOn = null,
                Notes = BuildProvisionedAgentNotes(technicalWorkspace.BindingSummary, selectedCandidate.RecommendationSummary, requestedBy),
                ExtendedDataJson = "{}",
                LastChangedBy = "process-launch-provisioning",
                Capabilities = technicalWorkspace.Profile.Capabilities.Count > 0
                    ? technicalWorkspace.Profile.Capabilities.ToList()
                    : capabilityMetadata
            },
            cancellationToken);
        if (saveProfileResult.IsFailure)
        {
            return Result<LaunchProvisioningOutcome>.Failure(saveProfileResult.Errors);
        }

        technicalWorkspace = await aiAgentService.GetAgentWorkspaceAsync(partyId.Value, cancellationToken);
        if (technicalWorkspace is null || !technicalWorkspace.TechnicalAgentId.HasValue)
        {
            return Result<LaunchProvisioningOutcome>.Failure(Error.Validation(
                "Provisioning did not create a usable technical agent binding.",
                "processes.launch.provisioning-binding-missing"));
        }

        var saveAssignmentResult = await projectPartyIntegrationBridge.SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = plan.ProjectId.Value,
                PartyId = partyId.Value,
                Role = role.PreferredExecutorKind.Contains("ai", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(role.PreferredExecutorKind, "AI agent", StringComparison.OrdinalIgnoreCase)
                    ? ProjectPartyAssignmentRole.AiAgent
                    : ProjectPartyAssignmentRole.AiAgent,
                AllocationPercent = role.IsRequired ? 100m : null,
                IsPrimary = false,
                Source = "process-launch-provisioning",
                Notes = $"Provisioned for launch plan '{plan.Name}'."
            },
            cancellationToken);
        if (saveAssignmentResult.IsFailure)
        {
            return Result<LaunchProvisioningOutcome>.Failure(saveAssignmentResult.Errors);
        }

        return Result<LaunchProvisioningOutcome>.Success(
            new LaunchProvisioningOutcome(
                partyId.Value,
                technicalWorkspace.TechnicalAgentId,
                "Provisioned technical AI resource and attached it to the launch plan."));
    }
}
