using CanDoItAll.Modules.Collaboration;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result> SubmitLaunchPlanForApprovalAsync(
        Guid launchPlanId,
        string requestedBy = "process-workspace",
        CancellationToken cancellationToken = default)
    {
        if (launchPlanId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Launch plan is required.", "processes.launch.plan-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);
        logger.LogInformation(
            "Submitting launch plan {LaunchPlanId} for approval. RequestedBy={RequestedBy}.",
            launchPlanId,
            requestedBy);

        try
        {
            var plan = await dbContext.Set<ProcessLaunchPlan>()
                .SingleOrDefaultAsync(item => item.Id == launchPlanId, cancellationToken);
            if (plan is null)
            {
                return Result.Failure(Error.Validation("Launch plan was not found.", "processes.launch.not-found"));
            }

            if (plan.Status is not ProcessLaunchPlanStatus.Draft and not ProcessLaunchPlanStatus.ChangesRequested)
            {
                return Result.Failure(Error.Validation(
                    "Only draft or changes-requested launch plans can be submitted for approval.",
                    "processes.launch.submit-locked"));
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

            foreach (var role in roles.Where(item => item.IsRequired))
            {
                if (!role.SelectedCandidateId.HasValue ||
                    !candidateLookup.TryGetValue(role.SelectedCandidateId.Value, out var selectedCandidate) ||
                    selectedCandidate.CandidateKind == ProcessLaunchCandidateKind.Gap)
                {
                    return Result.Failure(Error.Validation(
                        $"Required role '{role.DisplayName}' must select a resolvable candidate before approval can start.",
                        "processes.launch.required-role-unresolved"));
                }
            }

            var projectAssignments = plan.ProjectId.HasValue
                ? await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(plan.ProjectId.Value, cancellationToken)
                : [];
            var approvalAuthority = ResolveLaunchApprovalAuthority(projectAssignments);
            var requestMessage = BuildLaunchApprovalRequestMessage(
                plan,
                roles,
                candidateLookup,
                approvalAuthority,
                requestedBy);
            var contextRoute = BuildLaunchRoute(plan.ProcessDefinitionId, plan.ProjectId, plan.Id);
            logger.LogInformation(
                "Launch plan {LaunchPlanId} resolved approval authority {ApproverDisplayName} ({ApproverKind}). ExistingApprovalThread={HasApprovalThread}.",
                plan.Id,
                approvalAuthority.ApproverDisplayName,
                approvalAuthority.ApproverKind,
                plan.ApprovalThreadId.HasValue);

            Guid approvalThreadId;
            if (plan.ApprovalThreadId.HasValue)
            {
                approvalThreadId = plan.ApprovalThreadId.Value;
                var appendResult = await collaborationService.AppendMessageAsync(
                    dbContext,
                    new CollaborationMessageWriteRequest(
                        approvalThreadId,
                        $"process-launch:{plan.Id:D}",
                        "Process launch",
                        CollaborationMessageAuthorKind.System,
                        requestMessage,
                        CollaborationMessageKind.Escalation,
                        MarkAsUnread: true),
                    cancellationToken);
                if (appendResult.IsFailure)
                {
                    return Result.Failure(appendResult.Errors);
                }
            }
            else
            {
                logger.LogInformation(
                    "Launch plan {LaunchPlanId} is creating a new collaboration approval thread.",
                    plan.Id);
                var createThreadResult = await collaborationService.CreateThreadAsync(
                    dbContext,
                    new CollaborationThreadCreateRequest(
                        $"Launch approval / {plan.Name}",
                        CollaborationContextKind.ProcessLaunch,
                        plan.Id,
                        plan.ProjectId,
                        plan.Name,
                        contextRoute,
                        CollaborationInboxItemKind.Escalation,
                        $"process-launch:{plan.Id:D}",
                        "Process launch",
                        CollaborationParticipantKind.System,
                        requestMessage,
                        CollaborationMessageKind.Escalation,
                        MarkAsUnread: true),
                    cancellationToken);
                if (createThreadResult.IsFailure)
                {
                    return Result.Failure(createThreadResult.Errors);
                }

                approvalThreadId = createThreadResult.Value;
                plan.ApprovalThreadId = approvalThreadId;
                logger.LogInformation(
                    "Launch plan {LaunchPlanId} created collaboration approval thread {ApprovalThreadId}.",
                    plan.Id,
                    approvalThreadId);
            }

            var now = clock.GetUtcNow();
            var approvalRecord = new ProcessLaunchApprovalRecord
            {
                LaunchPlanId = plan.Id,
                Status = ProcessLaunchApprovalStatus.Pending,
                ApproverPartyId = approvalAuthority.ApproverPartyId,
                ApproverDisplayName = approvalAuthority.ApproverDisplayName,
                ApproverKind = approvalAuthority.ApproverKind,
                HumanSubstitutePartyId = approvalAuthority.HumanSubstitutePartyId,
                HumanSubstituteName = approvalAuthority.HumanSubstituteName,
                CollaborationThreadId = approvalThreadId,
                RequestMessage = requestMessage,
                ResolutionSummary = string.Empty,
                DecidedBy = string.Empty,
                CreatedAtUtc = now
            };

            await dbContext.Set<ProcessLaunchApprovalRecord>().AddAsync(approvalRecord, cancellationToken);
            plan.LatestApprovalRecordId = approvalRecord.Id;
            plan.Status = ProcessLaunchPlanStatus.PendingApproval;
            plan.SubmittedAtUtc = now;
            plan.UpdatedAtUtc = now;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Launch plan {LaunchPlanId} moved to PendingApproval with approval record {ApprovalRecordId}.",
                plan.Id,
                approvalRecord.Id);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(
                "Launch approval submission conflicted with another update. Reload and try again.",
                "processes.launch.submit-conflict"));
        }
    }

    public async Task<Result> DecideLaunchPlanApprovalAsync(
        ProcessLaunchApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LaunchPlanId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Launch plan is required.", "processes.launch.plan-required"));
        }

        if (request.Status is not ProcessLaunchApprovalStatus.Approved and
            not ProcessLaunchApprovalStatus.ChangesRequested and
            not ProcessLaunchApprovalStatus.Rejected)
        {
            return Result.Failure(Error.Validation(
                "Approval decisions must resolve to approved, changes requested, or rejected.",
                "processes.launch.approval-status-invalid"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);

        try
        {
            var plan = await dbContext.Set<ProcessLaunchPlan>()
                .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanId, cancellationToken);
            if (plan is null)
            {
                return Result.Failure(Error.Validation("Launch plan was not found.", "processes.launch.not-found"));
            }

            if (plan.Status != ProcessLaunchPlanStatus.PendingApproval || !plan.LatestApprovalRecordId.HasValue)
            {
                return Result.Failure(Error.Validation(
                    "Launch plan is not currently waiting for approval.",
                    "processes.launch.approval-not-pending"));
            }

            var approvalRecord = await dbContext.Set<ProcessLaunchApprovalRecord>()
                .SingleOrDefaultAsync(item => item.Id == plan.LatestApprovalRecordId.Value, cancellationToken);
            if (approvalRecord is null || approvalRecord.Status != ProcessLaunchApprovalStatus.Pending)
            {
                return Result.Failure(Error.Validation(
                    "The latest approval request is no longer pending.",
                    "processes.launch.approval-missing"));
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

            var now = clock.GetUtcNow();
            approvalRecord.Status = request.Status;
            approvalRecord.ResolutionSummary = request.ResolutionSummary.Trim();
            approvalRecord.DecidedBy = string.IsNullOrWhiteSpace(request.DecidedBy)
                ? "process-workspace"
                : request.DecidedBy.Trim();
            approvalRecord.DecidedAtUtc = now;

            if (approvalRecord.CollaborationThreadId.HasValue)
            {
                var decisionMessage = BuildLaunchApprovalDecisionMessage(plan, approvalRecord);
                var appendResult = await collaborationService.AppendMessageAsync(
                    dbContext,
                    new CollaborationMessageWriteRequest(
                        approvalRecord.CollaborationThreadId.Value,
                        $"launch-approval:{approvalRecord.Id:D}",
                        approvalRecord.DecidedBy,
                        CollaborationMessageAuthorKind.System,
                        decisionMessage,
                        request.Status == ProcessLaunchApprovalStatus.Rejected
                            ? CollaborationMessageKind.Escalation
                            : CollaborationMessageKind.Standard,
                        MarkAsUnread: true),
                    cancellationToken);
                if (appendResult.IsFailure)
                {
                    return Result.Failure(appendResult.Errors);
                }
            }

            if (request.Status == ProcessLaunchApprovalStatus.Approved)
            {
                var pendingProvisioningCount = 0;
                foreach (var role in roles)
                {
                    if (!role.SelectedCandidateId.HasValue ||
                        !candidateLookup.TryGetValue(role.SelectedCandidateId.Value, out var selectedCandidate))
                    {
                        continue;
                    }

                    role.RequiresProvisioning = selectedCandidate.RequiresProvisioning;
                    role.IsResolved = selectedCandidate.CandidateKind != ProcessLaunchCandidateKind.Gap;
                    role.SelectionSummary = ResolveLaunchSelectionSummary(selectedCandidate);
                    role.ReadinessSummary = selectedCandidate.RequiresProvisioning
                        ? ResolveLaunchReadinessSummary(selectedCandidate, "Approved")
                        : "Approved and ready for execution.";

                    if (!selectedCandidate.RequiresProvisioning)
                    {
                        continue;
                    }

                    pendingProvisioningCount++;
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
                        provisioningRequest.ResultPartyId = null;
                        provisioningRequest.ResultTechnicalAgentId = null;
                        provisioningRequest.ResultSummary = string.Empty;
                        provisioningRequest.CompletedAtUtc = null;
                    }
                }

                plan.ApprovedAtUtc = now;
                plan.Status = pendingProvisioningCount > 0
                    ? ProcessLaunchPlanStatus.Provisioning
                    : ProcessLaunchPlanStatus.Ready;
            }
            else
            {
                var staleProvisioning = await dbContext.Set<ProcessLaunchProvisioningRequest>()
                    .Where(item => item.LaunchPlanId == plan.Id && item.Status == ProcessLaunchProvisioningStatus.Pending)
                    .ToListAsync(cancellationToken);
                if (staleProvisioning.Count > 0)
                {
                    dbContext.RemoveRange(staleProvisioning);
                }

                plan.Status = request.Status == ProcessLaunchApprovalStatus.ChangesRequested
                    ? ProcessLaunchPlanStatus.ChangesRequested
                    : ProcessLaunchPlanStatus.Rejected;
            }

            plan.UpdatedAtUtc = now;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(
                "Launch approval resolution conflicted with another update. Reload and try again.",
                "processes.launch.approval-conflict"));
        }
    }

}
