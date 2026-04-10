using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private async Task<Result> SeedSoftwareDeliveryRuntimeAsync(
        ProcessSeedScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);

        if (stepRuns.Count < 9 || scenario.Roles.Count < 7)
        {
            return Result.Success();
        }

        var productOwnerRoleId = scenario.Roles[0].Id ?? Guid.Empty;
        var deliveryManagerRoleId = scenario.Roles[1].Id ?? Guid.Empty;
        var architectRoleId = scenario.Roles[2].Id ?? Guid.Empty;
        var engineerRoleId = scenario.Roles[3].Id ?? Guid.Empty;
        var qaLeadRoleId = scenario.Roles[4].Id ?? Guid.Empty;
        var securityReviewerRoleId = scenario.Roles[5].Id ?? Guid.Empty;
        var releaseManagerRoleId = scenario.Roles[6].Id ?? Guid.Empty;

        var assignmentResult = await EnsureAssignmentAsync(runId, assignments, productOwnerRoleId, null, "Lucia Marin", "person", "Product owner retained for the platform feature request.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, deliveryManagerRoleId, null, "David Polak", "person", "Delivery manager owns sequencing, scope pressure, and release commitments.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, architectRoleId, null, "CanDoItAll Architecture Analyst", "agent", "Architecture review can be prepared by the vetted internal architecture agent.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, engineerRoleId, null, "Natalia Kovac", "person", "Lead engineer assigned for code, migration, and rollout preparation.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, qaLeadRoleId, null, "Emilia Santos", "person", "QA lead retained for regression depth and release evidence quality.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, securityReviewerRoleId, null, string.Empty, "person", "Security review stays open because the shared reviewer pool is currently over capacity.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, releaseManagerRoleId, null, "Marek Ionescu", "person", "Release manager owns controlled rollout and rollback readiness.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }

        assignmentResult = await EnsureAssignmentAsync(runId, assignments, productOwnerRoleId, stepRuns[0].StepDefinitionId, "Lucia Marin", "person", "Owns scope boundary and acceptance timing for intake.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, architectRoleId, stepRuns[1].StepDefinitionId, "CanDoItAll Architecture Analyst", "agent", "Prepared the architecture decision pack and source-of-truth impact review.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, engineerRoleId, stepRuns[2].StepDefinitionId, "Natalia Kovac", "person", "Owns implementation, tests, and migration notes.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, architectRoleId, stepRuns[3].StepDefinitionId, "CanDoItAll Architecture Analyst", "agent", "Peer and integration review stays attached to the architecture authority.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, qaLeadRoleId, stepRuns[4].StepDefinitionId, "Emilia Santos", "person", "QA lead owns the regression evidence gate.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, securityReviewerRoleId, stepRuns[5].StepDefinitionId, string.Empty, "person", "Security reviewer is still not staffed for the data-handling exception review.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, deliveryManagerRoleId, stepRuns[6].StepDefinitionId, "David Polak", "person", "Delivery manager chairs the release approval board.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, releaseManagerRoleId, stepRuns[7].StepDefinitionId, "Marek Ionescu", "person", "Release manager executes the production rollout once the gate is cleared.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, deliveryManagerRoleId, stepRuns[8].StepDefinitionId, "David Polak", "person", "Delivery manager owns post-release learning capture.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }

        var transitionResult = await EnsureStepStatusAsync(runId, 0, ProcessStepRunStatus.Completed, "Scope intake was completed with acceptance boundaries, no-go constraints, and stakeholder list confirmed.", "seed-service/software-delivery", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 1, ProcessStepRunStatus.Completed, "Architecture review confirmed canonical-model boundaries, migration ownership, and integration guardrails.", "seed-service/software-delivery", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 2, ProcessStepRunStatus.Completed, "Implementation finished with code changes, migration notes, and automated validation evidence attached.", "seed-service/software-delivery", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 3, ProcessStepRunStatus.Completed, "Peer review closed the remaining integration questions and marked the change ready for QA.", "seed-service/software-delivery", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 4, ProcessStepRunStatus.Completed, "Regression evidence was accepted for the changed billing, project-workspace, and process authoring surfaces.", "seed-service/software-delivery", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 5, ProcessStepRunStatus.Blocked, "Security review is blocked because the shared reviewer pool has no capacity to validate the new tenant-data export exception before release.", "seed-service/software-delivery", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }

        var artifactResult = await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[1].Id,
            ProcessArtifactKind.Decision,
            "Architecture decision record for cross-module billing signal flow",
            ProcessArtifactTrustStatus.Approved,
            ProcessSensitivityLevel.Internal,
            "ADR approved during the seeded software-delivery architecture review.",
            "Reusable for implementation, review, and later forensic reconstruction of the selected design path.",
            "Approved architecture decision with reviewer sign-off and rejected-alternative summary.",
            cancellationToken);
        if (artifactResult.IsFailure) { return artifactResult; }

        artifactResult = await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[2].Id,
            ProcessArtifactKind.Deliverable,
            "Implementation change set and migration checklist",
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Generated from the seeded software-delivery implementation stage.",
            "Reusable for peer review, release approval, and release rehearsal.",
            "Human review still required before the deliverable may be promoted to release input.",
            cancellationToken);
        if (artifactResult.IsFailure) { return artifactResult; }

        artifactResult = await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[4].Id,
            ProcessArtifactKind.Evidence,
            "Regression and browser proof pack for billing workspace changes",
            ProcessArtifactTrustStatus.Approved,
            ProcessSensitivityLevel.Internal,
            "Validated regression pack with targeted tests and large-screen browser proof.",
            "Reusable for release approval, future diff review, and quality retrospectives.",
            "Approved by the seeded QA lead after coverage depth and defect triage review.",
            cancellationToken);
        if (artifactResult.IsFailure) { return artifactResult; }

        return await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[5].Id,
            ProcessArtifactKind.Decision,
            "Open security exception assessment for tenant export capability",
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessSensitivityLevel.Confidential,
            "Seeded exception record awaiting a staffed security reviewer.",
            "Reusable only for security review completion, release governance, and later audit.",
            "Cannot be approved automatically because the assigned reviewer role is currently unfilled.",
            cancellationToken);
    }

    private async Task<Result> SeedHotfixRuntimeAsync(
        ProcessSeedScenario scenario,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var stepRuns = await processesService.ListStepRunsAsync(runId, cancellationToken);
        var assignments = await processesService.ListAssignmentsAsync(runId, cancellationToken);
        var artifacts = await processesService.ListArtifactsAsync(runId, cancellationToken);

        if (stepRuns.Count < 7 || scenario.Roles.Count < 6)
        {
            return Result.Success();
        }

        var commanderRoleId = scenario.Roles[0].Id ?? Guid.Empty;
        var platformEngineerRoleId = scenario.Roles[1].Id ?? Guid.Empty;
        var databaseEngineerRoleId = scenario.Roles[2].Id ?? Guid.Empty;
        var qaResponderRoleId = scenario.Roles[3].Id ?? Guid.Empty;
        var releaseApproverRoleId = scenario.Roles[4].Id ?? Guid.Empty;
        var customerLiaisonRoleId = scenario.Roles[5].Id ?? Guid.Empty;

        var assignmentResult = await EnsureAssignmentAsync(runId, assignments, commanderRoleId, null, "Nina Korhonen", "person", "Incident commander for the emergency release lane.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, platformEngineerRoleId, null, "Pawel Zielinski", "person", "Platform engineer owns the hotfix package and release telemetry.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, databaseEngineerRoleId, null, "Riley Stone", "person", "Database engineer owns the shard-level migration and rollback script review.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, qaResponderRoleId, null, "Rafael Costa", "person", "QA responder validates the hotfix against the emergency regression checklist.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, releaseApproverRoleId, null, "Sofia Iliev", "person", "Release approver owns the emergency rollout go/no-go decision.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, customerLiaisonRoleId, null, "Helena Novak", "person", "Customer liaison owns outward-facing communications during the incident.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }

        assignmentResult = await EnsureAssignmentAsync(runId, assignments, commanderRoleId, stepRuns[0].StepDefinitionId, "Nina Korhonen", "person", "Incident commander acknowledges and classifies the production failure.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, databaseEngineerRoleId, stepRuns[1].StepDefinitionId, "Riley Stone", "person", "Database engineer validates blast radius and rollback options.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, platformEngineerRoleId, stepRuns[2].StepDefinitionId, "Pawel Zielinski", "person", "Platform engineer builds the emergency hotfix package.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, qaResponderRoleId, stepRuns[3].StepDefinitionId, "Rafael Costa", "person", "QA responder validates the hotfix in the shadow environment.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, releaseApproverRoleId, stepRuns[4].StepDefinitionId, "Sofia Iliev", "person", "Release approver owns the emergency rollout decision.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, platformEngineerRoleId, stepRuns[5].StepDefinitionId, "Pawel Zielinski", "person", "Platform engineer executes the rollout and telemetry verification.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }
        assignmentResult = await EnsureAssignmentAsync(runId, assignments, commanderRoleId, stepRuns[6].StepDefinitionId, "Nina Korhonen", "person", "Incident commander owns the post-incident review and learning capture.", false, cancellationToken);
        if (assignmentResult.IsFailure) { return assignmentResult; }

        var transitionResult = await EnsureStepStatusAsync(runId, 0, ProcessStepRunStatus.Completed, "The incident was acknowledged, customer impact classified, and the emergency bridge activated.", "seed-service/hotfix", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 1, ProcessStepRunStatus.Completed, "Blast radius analysis confirmed a shard-specific schema drift with rollback options prepared.", "seed-service/hotfix", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 2, ProcessStepRunStatus.Completed, "The hotfix package and rollback script bundle were produced for emergency validation.", "seed-service/hotfix", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 3, ProcessStepRunStatus.Completed, "Shadow-environment validation passed with one known residual risk on historical tenant shards.", "seed-service/hotfix", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 4, ProcessStepRunStatus.Completed, "Emergency rollout was approved with an explicit rollback trigger and customer-communication owner.", "seed-service/hotfix", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }
        transitionResult = await EnsureStepStatusAsync(runId, 5, ProcessStepRunStatus.Failed, "Production rollout failed because tenant shard 07 held a schema lock longer than the emergency change window allowed.", "seed-service/hotfix", cancellationToken);
        if (transitionResult.IsFailure) { return transitionResult; }

        var artifactResult = await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[3].Id,
            ProcessArtifactKind.Evidence,
            "Emergency shadow-environment validation pack",
            ProcessArtifactTrustStatus.Approved,
            ProcessSensitivityLevel.Internal,
            "Seeded validation pack for the emergency hotfix shadow run.",
            "Reusable for emergency release approval and later incident review.",
            "Approved by the seeded QA responder with residual-risk annotations.",
            cancellationToken);
        if (artifactResult.IsFailure) { return artifactResult; }

        return await EnsureArtifactAsync(
            runId,
            artifacts,
            stepRuns[5].Id,
            ProcessArtifactKind.Evidence,
            "Failed rollout telemetry capture and rollback trigger notes",
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessSensitivityLevel.Confidential,
            "Telemetry and operator notes captured at the point of rollout failure.",
            "Reusable for rollback review, forensic replay, and post-incident corrective action only.",
            "Needs human review because the telemetry stream contains production tenant identifiers.",
            cancellationToken);
    }
}
