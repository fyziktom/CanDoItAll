using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessDevelopmentSeedService(ProcessesService processesService)
{
    public async Task<Result<ProcessSeedReport>> SeedBaselineAsync(
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var seededDefinitionIds = new List<Guid>();
        var seededRunIds = new List<Guid>();

        var onboardingScenario = BuildOnboardingScenario();
        var authoringResult = await EnsureBaselineDefinitionAsync(
            onboardingScenario,
            projectId,
            seededDefinitionIds,
            seededRunIds,
            cancellationToken);
        if (authoringResult.IsFailure)
        {
            return Result<ProcessSeedReport>.Failure(authoringResult.Errors.ToArray());
        }

        var incidentScenario = BuildIncidentScenario();
        var supportResult = await EnsureBaselineDefinitionAsync(
            incidentScenario,
            projectId,
            seededDefinitionIds,
            seededRunIds,
            cancellationToken);
        if (supportResult.IsFailure)
        {
            return Result<ProcessSeedReport>.Failure(supportResult.Errors.ToArray());
        }

        return Result<ProcessSeedReport>.Success(new ProcessSeedReport(
            seededDefinitionIds,
            seededRunIds,
            authoringResult.Value,
            supportResult.Value));
    }

    private async Task<Result<Guid>> EnsureBaselineDefinitionAsync(
        ProcessSeedScenario scenario,
        Guid? projectId,
        ICollection<Guid> seededDefinitionIds,
        ICollection<Guid> seededRunIds,
        CancellationToken cancellationToken)
    {
        var existingDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Name, scenario.Name, StringComparison.OrdinalIgnoreCase));

        Guid definitionId;
        if (existingDefinition is not null)
        {
            definitionId = existingDefinition.Id;
        }
        else
        {
            var saveResult = await processesService.SaveAsync(
                new ProcessDefinitionEditorModel
                {
                    ProjectId = projectId,
                    Name = scenario.Name,
                    Summary = scenario.Summary,
                    ValueStatement = scenario.ValueStatement,
                    CustomerName = scenario.CustomerName,
                    OwnerName = scenario.OwnerName,
                    InterfaceContractSummary = scenario.InterfaceContractSummary,
                    GovernanceNotes = scenario.GovernanceNotes,
                    ChangeSummary = scenario.ChangeSummary,
                    GovernancePolicySummary = scenario.GovernancePolicySummary,
                    ConstitutionRuleSummary = scenario.ConstitutionRuleSummary,
                    OperatingModeSummary = scenario.OperatingModeSummary,
                    SimulationReadinessSummary = scenario.SimulationReadinessSummary,
                    Roles = scenario.Roles,
                    Steps = scenario.Steps
                },
                cancellationToken);
            if (saveResult.IsFailure)
            {
                return Result<Guid>.Failure(saveResult.Errors.ToArray());
            }

            definitionId = saveResult.Value;
        }

        seededDefinitionIds.Add(definitionId);

        var refreshedDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .First(item => item.Id == definitionId);
        if (!refreshedDefinition.HasPublishedVersion)
        {
            var publishResult = await processesService.PublishAsync(definitionId, cancellationToken);
            if (publishResult.IsFailure)
            {
                return Result<Guid>.Failure(publishResult.Errors.ToArray());
            }
        }

        var existingRun = (await processesService.ListRunsAsync(definitionId, projectId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Name, scenario.RunName, StringComparison.OrdinalIgnoreCase));
        if (existingRun is not null)
        {
            seededRunIds.Add(existingRun.Id);
            return Result<Guid>.Success(definitionId);
        }

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = definitionId,
                ProjectId = projectId,
                RunName = scenario.RunName,
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Development seed baseline"
            },
            cancellationToken);
        if (runResult.IsFailure)
        {
            return Result<Guid>.Failure(runResult.Errors.ToArray());
        }

        seededRunIds.Add(runResult.Value);
        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value, cancellationToken);
        if (stepRuns.Count > 0)
        {
            var startResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRuns[0].Id,
                    TargetStatus = ProcessStepRunStatus.InProgress,
                    Reason = "Seed flow started.",
                    DecidedBy = "seed-service"
                },
                cancellationToken);
            if (startResult.IsFailure)
            {
                return Result<Guid>.Failure(startResult.Errors.ToArray());
            }

            var completeResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRuns[0].Id,
                    TargetStatus = ProcessStepRunStatus.Completed,
                    Reason = "Seed flow completed initial intake.",
                    DecidedBy = "seed-service"
                },
                cancellationToken);
            if (completeResult.IsFailure)
            {
                return Result<Guid>.Failure(completeResult.Errors.ToArray());
            }
        }

        var refreshedStepRuns = await processesService.ListStepRunsAsync(runResult.Value, cancellationToken);
        if (refreshedStepRuns.Count > 1)
        {
            var blockResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = refreshedStepRuns[1].Id,
                    TargetStatus = ProcessStepRunStatus.Blocked,
                    Reason = "Seeded blocking scenario for validation and analytics coverage.",
                    DecidedBy = "seed-service"
                },
                cancellationToken);
            if (blockResult.IsFailure)
            {
                return Result<Guid>.Failure(blockResult.Errors.ToArray());
            }

            var artifactResult = await processesService.RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = runResult.Value,
                    StepRunId = refreshedStepRuns[1].Id,
                    ArtifactKind = ProcessArtifactKind.Evidence,
                    Title = $"{scenario.Name} seed evidence",
                    TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                    SensitivityLevel = ProcessSensitivityLevel.Internal,
                    ProvenanceSummary = "Generated by ProcessDevelopmentSeedService.",
                    AllowedFutureUsageSummary = "Development and regression validation only.",
                    ReviewSummary = "Needs manual confirmation before reuse."
                },
                cancellationToken);
            if (artifactResult.IsFailure)
            {
                return Result<Guid>.Failure(artifactResult.Errors.ToArray());
            }
        }

        return Result<Guid>.Success(definitionId);
    }

    private static ProcessSeedScenario BuildOnboardingScenario()
    {
        var accountOwnerId = Guid.NewGuid();
        var staffingManagerId = Guid.NewGuid();
        var kickoffLeadId = Guid.NewGuid();

        var roles = new List<ProcessRoleEditorModel>
        {
            new()
            {
                Id = accountOwnerId,
                Key = "account-owner",
                DisplayName = "Account owner",
                Purpose = "Own the customer relationship and demand context.",
                StaffingIntent = "Primary commercial owner for the customer account.",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.CustomerContact,
                PreferredExecutorKind = "person",
                SnapshotSummary = "Commercial owner with customer context."
            },
            new()
            {
                Id = staffingManagerId,
                Key = "staffing-manager",
                DisplayName = "Staffing manager",
                Purpose = "Provide delivery-capable staffing options.",
                StaffingIntent = "Delivery-side staffing authority.",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                PreferredExecutorKind = "person",
                SnapshotSummary = "Delivery staffing authority."
            },
            new()
            {
                Id = kickoffLeadId,
                Key = "kickoff-lead",
                DisplayName = "Kickoff lead",
                Purpose = "Accept the baton and prepare the project kickoff.",
                StaffingIntent = "Project-level delivery leader.",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.TeamMember,
                PreferredExecutorKind = "person",
                SnapshotSummary = "Delivery lead for kickoff readiness."
            }
        };

        var intakeId = Guid.NewGuid();
        var staffingReviewId = Guid.NewGuid();

        var steps = new List<ProcessStepEditorModel>
        {
            new()
            {
                Id = intakeId,
                Key = "intake",
                Title = "Capture commercial intake",
                StepKind = ProcessStepKind.Start,
                InputContractSummary = "Signed scope, target dates, and stakeholder summary.",
                OutputContractSummary = "Typed intake packet ready for delivery review.",
                EvidenceContractSummary = "Scope summary and decision-ready notes.",
                DecisionRightsSummary = "Account owner can prepare intake but cannot commit delivery without review.",
                TargetLeadHours = 4,
                CanvasX = 120,
                CanvasY = 140,
                RoleAssignments =
                [
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = accountOwnerId,
                        ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                        RebindPolicySummary = "Rebind to another commercial owner if the original owner changes."
                    }
                ]
            },
            new()
            {
                Id = staffingReviewId,
                Key = "staffing-review",
                Title = "Review staffing intent",
                StepKind = ProcessStepKind.Review,
                RequiresDecisionRecord = true,
                InputContractSummary = "Intake packet and delivery constraints.",
                OutputContractSummary = "Recommended staffing path with explicit fallback.",
                EvidenceContractSummary = "Candidate list and fallback recommendation.",
                DecisionRightsSummary = "Staffing manager recommends; governance owner approves.",
                TargetLeadHours = 8,
                DependsOnStepId = intakeId,
                CanvasX = 420,
                CanvasY = 140,
                RoleAssignments =
                [
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = staffingManagerId,
                        ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                        RebindPolicySummary = "Rebind to another staffing manager if allocation changes."
                    },
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = kickoffLeadId,
                        ResponsibilityKind = ProcessResponsibilityKind.Reviewer,
                        RebindPolicySummary = "Delivery lead review stays explicit even if assigned person changes."
                    }
                ],
                ArtifactExpectations =
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Brief,
                        Title = "Staffing recommendation",
                        ValidationRequirementSummary = "Reviewer must confirm role-fit and availability."
                    }
                ]
            },
            new()
            {
                Key = "kickoff-approval",
                Title = "Approve kickoff readiness",
                StepKind = ProcessStepKind.Approval,
                RequiresApproval = true,
                RequiresDecisionRecord = true,
                InputContractSummary = "Staffing recommendation and draft kickoff plan.",
                OutputContractSummary = "Approved or rejected kickoff readiness.",
                EvidenceContractSummary = "Approval record and managed artifacts.",
                DecisionRightsSummary = "Governance owner can approve, block, or refuse unsafe launch.",
                TargetLeadHours = 2,
                DependsOnStepId = staffingReviewId,
                CanvasX = 720,
                CanvasY = 140,
                RoleAssignments =
                [
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = kickoffLeadId,
                        ResponsibilityKind = ProcessResponsibilityKind.Approver,
                        RebindPolicySummary = "Approval remains attached to the role, not the current person."
                    }
                ],
                ArtifactExpectations =
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Decision,
                        Title = "Kickoff approval record",
                        ValidationRequirementSummary = "Approval rationale must be explicit and reviewable."
                    }
                ]
            }
        };

        return new ProcessSeedScenario(
            "Customer onboarding orchestration",
            "Customer onboarding orchestration / Seed run",
            "Turn approved customer demand into a governed delivery kickoff without losing staffing, approvals, or evidence.",
            "Reduce handoff loss across sales, staffing, approval, and kickoff preparation.",
            "Customer Success",
            "Operations governance board",
            "Sales hands over to delivery through explicit contracts and approval gates.",
            "All external commitments require accountable owner review before kickoff.",
            "Published v1 for development and regression validation.",
            "Kickoff readiness depends on explicit policy, accountability, and evidence retention.",
            "Policy decisions stay explicit and reviewable.",
            "Manual and guarded autonomy are both supported depending on step criticality.",
            "Seed pack models the same typed steps used in runtime validation.",
            roles,
            steps);
    }

    private static ProcessSeedScenario BuildIncidentScenario()
    {
        var triageLeadId = Guid.NewGuid();
        var resolverId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var roles = new List<ProcessRoleEditorModel>
        {
            new()
            {
                Id = triageLeadId,
                Key = "triage-lead",
                DisplayName = "Triage lead",
                Purpose = "Own first response and initial diagnosis.",
                StaffingIntent = "Front-line responder with customer communication authority.",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                PreferredExecutorKind = "person",
                SnapshotSummary = "First-response operations lead."
            },
            new()
            {
                Id = resolverId,
                Key = "resolver",
                DisplayName = "Resolver",
                Purpose = "Drive technical diagnosis and recovery.",
                StaffingIntent = "Technical resolver or responsible agent.",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.TeamMember,
                PreferredExecutorKind = "person-or-agent",
                SnapshotSummary = "Technical execution owner."
            },
            new()
            {
                Id = approverId,
                Key = "approver",
                DisplayName = "Escalation approver",
                Purpose = "Approve emergency changes and non-standard paths.",
                StaffingIntent = "Governance approver for risky transitions.",
                PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Reviewer,
                PreferredExecutorKind = "person",
                SnapshotSummary = "Explicit governance approval owner."
            }
        };

        var respondId = Guid.NewGuid();
        var diagnoseId = Guid.NewGuid();

        var steps = new List<ProcessStepEditorModel>
        {
            new()
            {
                Id = respondId,
                Key = "respond",
                Title = "Acknowledge and classify incident",
                StepKind = ProcessStepKind.Start,
                AllowsSafeRefusal = true,
                InputContractSummary = "Inbound alert, customer impact, and available evidence.",
                OutputContractSummary = "Initial severity and response owner.",
                EvidenceContractSummary = "Timestamped acknowledgement and incident notes.",
                DecisionRightsSummary = "Triage lead may safely refuse malformed or irrelevant incidents.",
                TargetLeadHours = 1,
                CanvasX = 120,
                CanvasY = 320,
                RoleAssignments =
                [
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = triageLeadId,
                        ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                        RebindPolicySummary = "Another triage lead may take over without invalidating the process."
                    }
                ]
            },
            new()
            {
                Id = diagnoseId,
                Key = "diagnose",
                Title = "Diagnose probable cause",
                StepKind = ProcessStepKind.Work,
                RequiresDecisionRecord = true,
                InputContractSummary = "Initial severity and evidence.",
                OutputContractSummary = "Diagnosis hypothesis and proposed action.",
                EvidenceContractSummary = "Logs, traces, or structured findings.",
                DecisionRightsSummary = "Resolver proposes action; approver decides emergency changes.",
                TargetLeadHours = 6,
                DependsOnStepId = respondId,
                CanvasX = 420,
                CanvasY = 320,
                RoleAssignments =
                [
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = resolverId,
                        ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                        RebindPolicySummary = "Resolver role may be rebound to another capable executor."
                    },
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = triageLeadId,
                        ResponsibilityKind = ProcessResponsibilityKind.Reviewer,
                        RebindPolicySummary = "Triage lead validates communication impact before escalation."
                    }
                ],
                ArtifactExpectations =
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Evidence,
                        Title = "Diagnosis evidence pack",
                        ValidationRequirementSummary = "Evidence must capture source and review status."
                    }
                ]
            },
            new()
            {
                Key = "escalate",
                Title = "Approve escalation path",
                StepKind = ProcessStepKind.Approval,
                RequiresApproval = true,
                InputContractSummary = "Diagnosis and proposed change path.",
                OutputContractSummary = "Approved, blocked, or refused escalation.",
                EvidenceContractSummary = "Approval record and rationale.",
                DecisionRightsSummary = "Approver owns the escalation gate.",
                TargetLeadHours = 2,
                DependsOnStepId = diagnoseId,
                CanvasX = 720,
                CanvasY = 320,
                RoleAssignments =
                [
                    new ProcessStepRoleRequirementEditorModel
                    {
                        RoleRequirementId = approverId,
                        ResponsibilityKind = ProcessResponsibilityKind.Approver,
                        RebindPolicySummary = "Escalation approval always belongs to the current approver role holder."
                    }
                ],
                ArtifactExpectations =
                [
                    new ProcessArtifactExpectationEditorModel
                    {
                        ArtifactKind = ProcessArtifactKind.Decision,
                        Title = "Escalation approval record",
                        ValidationRequirementSummary = "Emergency change approvals must keep explicit rationale."
                    }
                ]
            }
        };

        return new ProcessSeedScenario(
            "Incident response and escalation",
            "Incident response and escalation / Seed run",
            "Coordinate first response, diagnosis, escalation, and customer communication with explicit safe-refusal paths.",
            "Shorten blocked time and preserve trust under ambiguity.",
            "Managed services",
            "Response leadership",
            "Customer-facing response depends on reliable diagnosis and explicit decision rights.",
            "Critical escalations require approval notes and trust-aware evidence handling.",
            "Published v1 for high-signal runtime and conformance tests.",
            "Emergency paths stay bounded by approval, journaling, and evidence controls.",
            "Policy decisions stay explicit and reviewable.",
            "Emergency operating mode is explicitly bounded by governance rules.",
            "Seed pack models refusal, blocking, and artifact trust scenarios.",
            roles,
            steps);
    }
}

public sealed record ProcessSeedReport(
    IReadOnlyCollection<Guid> SeededDefinitionIds,
    IReadOnlyCollection<Guid> SeededRunIds,
    Guid PrimaryDefinitionId,
    Guid SecondaryDefinitionId);

internal sealed record ProcessSeedScenario(
    string Name,
    string RunName,
    string Summary,
    string ValueStatement,
    string CustomerName,
    string OwnerName,
    string InterfaceContractSummary,
    string GovernanceNotes,
    string ChangeSummary,
    string GovernancePolicySummary,
    string ConstitutionRuleSummary,
    string OperatingModeSummary,
    string SimulationReadinessSummary,
    List<ProcessRoleEditorModel> Roles,
    List<ProcessStepEditorModel> Steps);
