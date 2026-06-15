using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessWorkflowRepairDefinitionTestFixture
{
    public static class StepKeys
    {
        public const string Scope = "sample-scope";
        public const string Architecture = "sample-architecture";
        public const string FirstImplementation = "sample-first-implementation";
        public const string QaFirstReview = "sample-qa-first-review";
        public const string DirectReleaseNotes = "sample-direct-release-notes";
        public const string RepairImplementation = "sample-repair-implementation";
        public const string QaRecheck = "sample-qa-recheck";
        public const string ReleaseNotes = "sample-release-notes";
    }

    public static class BranchOutcomeKeys
    {
        public const string RepairsRequired = ProcessMockAgentCatalog.BranchRepairsRequired;
        public const string Approved = ProcessMockAgentCatalog.BranchApproved;
    }

    public static class ArtifactTitles
    {
        public const string Scope = "Sample scope artifact";
        public const string Architecture = "Sample architecture artifact";
        public const string FirstImplementation = "Sample first implementation artifact";
        public const string QaFirstReview = "Sample QA rejection artifact";
        public const string DirectReleaseNotes = "Sample direct release notes artifact";
        public const string RepairImplementation = "Sample repair artifact";
        public const string QaRecheck = "Sample QA approval artifact";
        public const string ReleaseNotes = "Sample release notes artifact";
    }

    public static WorkflowRepairProcessDefinitionFixture Create(Guid projectId)
    {
        var productOwnerRoleId = Guid.NewGuid();
        var architectRoleId = Guid.NewGuid();
        var developerRoleId = Guid.NewGuid();
        var qaRoleId = Guid.NewGuid();
        var repairDeveloperRoleId = Guid.NewGuid();
        var releaseManagerRoleId = Guid.NewGuid();

        var scopeStepId = Guid.NewGuid();
        var architectureStepId = Guid.NewGuid();
        var firstImplementationStepId = Guid.NewGuid();
        var qaFirstReviewStepId = Guid.NewGuid();
        var directReleaseNotesStepId = Guid.NewGuid();
        var repairImplementationStepId = Guid.NewGuid();
        var qaRecheckStepId = Guid.NewGuid();
        var releaseNotesStepId = Guid.NewGuid();

        var firstQaRepairsRequiredOutcomeId = Guid.NewGuid();
        var firstQaApprovedOutcomeId = Guid.NewGuid();
        var qaRecheckApprovedOutcomeId = Guid.NewGuid();

        var scopeArtifactId = Guid.NewGuid();
        var architectureArtifactId = Guid.NewGuid();
        var firstImplementationArtifactId = Guid.NewGuid();
        var qaFirstReviewArtifactId = Guid.NewGuid();
        var directReleaseNotesArtifactId = Guid.NewGuid();
        var repairImplementationArtifactId = Guid.NewGuid();
        var qaRecheckArtifactId = Guid.NewGuid();
        var releaseNotesArtifactId = Guid.NewGuid();

        var editor = new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Deterministic sample QA repair process",
            Summary = "Exercises sample scope, implementation, QA rejection, repair, QA approval, and release notes.",
            ValueStatement = "Keep the mock-agent process path deterministic enough to prove branch and artifact contracts.",
            CustomerName = "Acme Customer",
            OwnerName = "Process Mock Product Owner",
            GovernancePolicySummary = "Every automated step must persist the required process artifact before completion.",
            ChangeSummary = "Initial deterministic sample repair-loop definition.",
            ConstitutionRuleSummary = "QA branch outcomes must route repair and approval paths explicitly.",
            OperatingModeSummary = "Assisted mock-agent execution with governed step completion.",
            SimulationReadinessSummary = "Safe for integration validation without real LLM calls.",
            Roles =
            [
                CreateRole(
                    productOwnerRoleId,
                    ProcessMockAgentRoleKeys.ProductOwner,
                    "Product Owner",
                    "Own sample scope and acceptance criteria.",
                    "Process mock product owner for deterministic scope generation."),
                CreateRole(
                    architectRoleId,
                    ProcessMockAgentRoleKeys.Architect,
                    "Solution Architect",
                    "Own sample architecture constraints.",
                    "Process mock architect for deterministic architecture guidance."),
                CreateRole(
                    developerRoleId,
                    ProcessMockAgentRoleKeys.Developer,
                    "Developer",
                    "Own the first sample implementation.",
                    "Process mock developer for deterministic first-pass implementation."),
                CreateRole(
                    qaRoleId,
                    ProcessMockAgentRoleKeys.Qa,
                    "QA Reviewer",
                    "Own sample QA rejection and approval decisions.",
                    "Process mock QA reviewer for deterministic repair-loop branch selection."),
                CreateRole(
                    repairDeveloperRoleId,
                    ProcessMockAgentRoleKeys.RepairDeveloper,
                    "Repair Developer",
                    "Own sample defect repair.",
                    "Process mock repair developer for deterministic blank-input fix."),
                CreateRole(
                    releaseManagerRoleId,
                    ProcessMockAgentRoleKeys.ReleaseManager,
                    "Release Manager",
                    "Own release note preparation after QA approval.",
                    "Process mock release manager for deterministic release notes.")
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = scopeStepId,
                    Key = StepKeys.Scope,
                    Title = "Write sample scope",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Sample product request.",
                    OutputContractSummary = "Scope and acceptance criteria.",
                    EvidenceContractSummary = "Scope artifact persisted for downstream implementation.",
                    DecisionRightsSummary = "Product owner owns scope completion.",
                    ExceptionPolicySummary = "Block when acceptance criteria are missing.",
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(productOwnerRoleId)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            scopeArtifactId,
                            ProcessArtifactKind.Brief,
                            ArtifactTitles.Scope,
                            "Scope artifact must describe validation behavior and blank-input acceptance criteria.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = architectureStepId,
                    Key = StepKeys.Architecture,
                    Title = "Write sample architecture",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((scopeStepId, null)),
                    InputContractSummary = "Accepted sample scope.",
                    OutputContractSummary = "Small application architecture constraints.",
                    EvidenceContractSummary = "Architecture artifact persisted for developer handoff.",
                    DecisionRightsSummary = "Architect owns architecture completion.",
                    ExceptionPolicySummary = "Block when implementation constraints are unclear.",
                    TargetLeadHours = 1,
                    CanvasX = 420,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(architectRoleId)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            architectureArtifactId,
                            ProcessArtifactKind.Decision,
                            ArtifactTitles.Architecture,
                            "Architecture artifact must define the sample boundary and QA expectations.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = firstImplementationStepId,
                    Key = StepKeys.FirstImplementation,
                    Title = "Write first sample implementation",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((architectureStepId, null)),
                    InputContractSummary = "Architecture constraints and scope.",
                    OutputContractSummary = "First-pass implementation with deterministic QA defect.",
                    EvidenceContractSummary = "Implementation artifact persisted before QA review.",
                    DecisionRightsSummary = "Developer owns implementation completion.",
                    ExceptionPolicySummary = "Block when the implementation artifact is missing.",
                    TargetLeadHours = 2,
                    CanvasX = 700,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(developerRoleId)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            firstImplementationArtifactId,
                            ProcessArtifactKind.Deliverable,
                            ArtifactTitles.FirstImplementation,
                            "First implementation artifact must identify the deterministic blank-input defect.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = qaFirstReviewStepId,
                    Key = StepKeys.QaFirstReview,
                    Title = "Review first sample implementation",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((firstImplementationStepId, null)),
                    DecisionRoleRequirementId = qaRoleId,
                    InputContractSummary = "First implementation artifact.",
                    OutputContractSummary = "QA disposition choosing approval or repair.",
                    EvidenceContractSummary = "QA review artifact persisted before branch selection.",
                    DecisionRightsSummary = "QA reviewer selects the branch outcome.",
                    ExceptionPolicySummary = "Artifact recovery: block when the required QA review artifact is missing or invalid before selecting approval or repair.",
                    TargetLeadHours = 1,
                    CanvasX = 980,
                    CanvasY = 160,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = firstQaRepairsRequiredOutcomeId,
                            Key = BranchOutcomeKeys.RepairsRequired,
                            Title = "Repairs required",
                            Description = "Route the sample implementation through defect repair."
                        },
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = firstQaApprovedOutcomeId,
                            Key = BranchOutcomeKeys.Approved,
                            Title = "Approved",
                            Description = "Route directly to release notes when no repair is required."
                        }
                    ],
                    RoleAssignments =
                    [
                        CreateRoleAssignment(qaRoleId, ProcessResponsibilityKind.Reviewer)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            qaFirstReviewArtifactId,
                            ProcessArtifactKind.Evidence,
                            ArtifactTitles.QaFirstReview,
                            "QA first review artifact must record the branch reason.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = directReleaseNotesStepId,
                    Key = StepKeys.DirectReleaseNotes,
                    Title = "Write direct sample release notes",
                    StepKind = ProcessStepKind.Delivery,
                    Dependencies = CreateDependencies((qaFirstReviewStepId, firstQaApprovedOutcomeId)),
                    InputContractSummary = "First QA approval.",
                    OutputContractSummary = "Release notes for the no-repair path.",
                    EvidenceContractSummary = "Direct release notes artifact persisted.",
                    DecisionRightsSummary = "Release manager owns direct release note completion.",
                    ExceptionPolicySummary = "Skip when QA selects the repair path.",
                    TargetLeadHours = 1,
                    CanvasX = 1260,
                    CanvasY = 80,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(releaseManagerRoleId)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            directReleaseNotesArtifactId,
                            ProcessArtifactKind.Deliverable,
                            ArtifactTitles.DirectReleaseNotes,
                            "Direct release notes artifact is required only for the first-pass approval path.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = repairImplementationStepId,
                    Key = StepKeys.RepairImplementation,
                    Title = "Repair sample implementation",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((qaFirstReviewStepId, firstQaRepairsRequiredOutcomeId)),
                    InputContractSummary = "QA rejection and first implementation artifact.",
                    OutputContractSummary = "Repaired sample implementation.",
                    EvidenceContractSummary = "Repair artifact persisted before QA recheck.",
                    DecisionRightsSummary = "Repair developer owns defect correction.",
                    ExceptionPolicySummary = "Block when the repaired implementation artifact is missing.",
                    TargetLeadHours = 2,
                    CanvasX = 1260,
                    CanvasY = 240,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(repairDeveloperRoleId)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            repairImplementationArtifactId,
                            ProcessArtifactKind.Deliverable,
                            ArtifactTitles.RepairImplementation,
                            "Repair artifact must document the blank-input fix.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = qaRecheckStepId,
                    Key = StepKeys.QaRecheck,
                    Title = "Recheck repaired sample implementation",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((repairImplementationStepId, null)),
                    DecisionRoleRequirementId = qaRoleId,
                    InputContractSummary = "Repair artifact and QA rejection.",
                    OutputContractSummary = "QA approval branch decision.",
                    EvidenceContractSummary = "QA approval artifact persisted before release.",
                    DecisionRightsSummary = "QA reviewer selects approval after recheck.",
                    ExceptionPolicySummary = "Artifact recovery: block when the required QA recheck artifact is missing or invalid before selecting the repaired approval branch.",
                    TargetLeadHours = 1,
                    CanvasX = 1540,
                    CanvasY = 240,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = qaRecheckApprovedOutcomeId,
                            Key = BranchOutcomeKeys.Approved,
                            Title = "Approved",
                            Description = "Route repaired sample implementation to release notes."
                        }
                    ],
                    RoleAssignments =
                    [
                        CreateRoleAssignment(qaRoleId, ProcessResponsibilityKind.Reviewer)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            qaRecheckArtifactId,
                            ProcessArtifactKind.Evidence,
                            ArtifactTitles.QaRecheck,
                            "QA recheck artifact must record approval for release.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = releaseNotesStepId,
                    Key = StepKeys.ReleaseNotes,
                    Title = "Write sample release notes",
                    StepKind = ProcessStepKind.Delivery,
                    Dependencies = CreateDependencies((qaRecheckStepId, qaRecheckApprovedOutcomeId)),
                    InputContractSummary = "QA approval after repair.",
                    OutputContractSummary = "Final release notes.",
                    EvidenceContractSummary = "Release notes artifact persisted after QA approval.",
                    DecisionRightsSummary = "Release manager owns release note completion.",
                    ExceptionPolicySummary = "Block when approval evidence is missing.",
                    TargetLeadHours = 1,
                    CanvasX = 1820,
                    CanvasY = 240,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(releaseManagerRoleId)
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            releaseNotesArtifactId,
                            ProcessArtifactKind.Deliverable,
                            ArtifactTitles.ReleaseNotes,
                            "Release notes artifact must summarize scope, repair evidence, QA approval, and residual risk.")
                    ]
                }
            ]
        };

        var stepIdsByKey = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            [StepKeys.Scope] = scopeStepId,
            [StepKeys.Architecture] = architectureStepId,
            [StepKeys.FirstImplementation] = firstImplementationStepId,
            [StepKeys.QaFirstReview] = qaFirstReviewStepId,
            [StepKeys.DirectReleaseNotes] = directReleaseNotesStepId,
            [StepKeys.RepairImplementation] = repairImplementationStepId,
            [StepKeys.QaRecheck] = qaRecheckStepId,
            [StepKeys.ReleaseNotes] = releaseNotesStepId
        };
        var branchOutcomeIdsByStepAndKey = new Dictionary<(string StepKey, string OutcomeKey), Guid>
        {
            [(StepKeys.QaFirstReview, BranchOutcomeKeys.RepairsRequired)] = firstQaRepairsRequiredOutcomeId,
            [(StepKeys.QaFirstReview, BranchOutcomeKeys.Approved)] = firstQaApprovedOutcomeId,
            [(StepKeys.QaRecheck, BranchOutcomeKeys.Approved)] = qaRecheckApprovedOutcomeId
        };
        var artifactExpectationIdsByStepKey = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            [StepKeys.Scope] = scopeArtifactId,
            [StepKeys.Architecture] = architectureArtifactId,
            [StepKeys.FirstImplementation] = firstImplementationArtifactId,
            [StepKeys.QaFirstReview] = qaFirstReviewArtifactId,
            [StepKeys.DirectReleaseNotes] = directReleaseNotesArtifactId,
            [StepKeys.RepairImplementation] = repairImplementationArtifactId,
            [StepKeys.QaRecheck] = qaRecheckArtifactId,
            [StepKeys.ReleaseNotes] = releaseNotesArtifactId
        };

        return new WorkflowRepairProcessDefinitionFixture(
            editor,
            stepIdsByKey,
            branchOutcomeIdsByStepAndKey,
            artifactExpectationIdsByStepKey);
    }

    private static ProcessRoleEditorModel CreateRole(
        Guid roleId,
        string roleKey,
        string displayName,
        string purpose,
        string staffingIntent)
    {
        return new ProcessRoleEditorModel
        {
            Id = roleId,
            Key = roleKey,
            DisplayName = displayName,
            Purpose = purpose,
            StaffingIntent = staffingIntent,
            PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.AiAgent,
            PreferredExecutorKind = "ai-agent",
            RoleTemplateSourceKey = ProcessMockAgentCatalog.CreateRoleTag(roleKey),
            RoleTemplateSnapshotName = ProcessMockAgentCatalog.AgentTag,
            SnapshotSummary = $"{displayName} process mock role."
        };
    }

    private static ProcessStepRoleRequirementEditorModel CreateRoleAssignment(
        Guid roleId,
        ProcessResponsibilityKind responsibilityKind = ProcessResponsibilityKind.Responsible)
    {
        return new ProcessStepRoleRequirementEditorModel
        {
            RoleRequirementId = roleId,
            ResponsibilityKind = responsibilityKind,
            RebindPolicySummary = "Bind to the deterministic process mock agent for this role."
        };
    }

    private static ProcessArtifactExpectationEditorModel CreateArtifactExpectation(
        Guid artifactExpectationId,
        ProcessArtifactKind artifactKind,
        string title,
        string validationRequirementSummary)
    {
        return new ProcessArtifactExpectationEditorModel
        {
            Id = artifactExpectationId,
            ArtifactKind = artifactKind,
            Title = title,
            ValidationRequirementSummary = validationRequirementSummary,
            AllowedFutureUsageSummary = "Regression proof for deterministic process mock execution only."
        };
    }

    private static List<ProcessStepDependencyEditorModel> CreateDependencies(params (Guid StepId, Guid? BranchOutcomeId)[] items)
    {
        return items
            .Select(item => new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = item.StepId,
                DependsOnBranchOutcomeId = item.BranchOutcomeId
            })
            .ToList();
    }
}

internal sealed record WorkflowRepairProcessDefinitionFixture(
    ProcessDefinitionEditorModel Editor,
    IReadOnlyDictionary<string, Guid> StepIdsByKey,
    IReadOnlyDictionary<(string StepKey, string OutcomeKey), Guid> BranchOutcomeIdsByStepAndKey,
    IReadOnlyDictionary<string, Guid> ArtifactExpectationIdsByStepKey)
{
    public Guid StepId(string stepKey)
    {
        return StepIdsByKey[stepKey];
    }

    public Guid BranchOutcomeId(string stepKey, string outcomeKey)
    {
        return BranchOutcomeIdsByStepAndKey[(stepKey, outcomeKey)];
    }

    public Guid ArtifactExpectationId(string stepKey)
    {
        return ArtifactExpectationIdsByStepKey[stepKey];
    }
}
