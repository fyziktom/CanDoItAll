using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Integration;

internal static class ProcessMockThreeAgentArtifactHandoffFixture
{
    public static class StepKeys
    {
        public const string Scope = "three-agent-scope";
        public const string Implementation = "three-agent-implementation";
        public const string Review = "three-agent-review";
    }

    public static class ArtifactTitles
    {
        public const string Scope = "Mock scope artifact";
        public const string ImplementationChangeSet = "Implementation change set";
        public const string MigrationRolloutChecklist = "Migration and rollout preparation checklist";
        public const string QaApproval = "Mock QA approval artifact";
    }

    public static ThreeAgentArtifactHandoffProcessDefinitionFixture Create(Guid projectId)
    {
        var productOwnerRoleId = Guid.NewGuid();
        var developerRoleId = Guid.NewGuid();
        var qaRoleId = Guid.NewGuid();

        var scopeStepId = Guid.NewGuid();
        var implementationStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();

        var scopeArtifactId = Guid.NewGuid();
        var implementationChangeSetArtifactId = Guid.NewGuid();
        var migrationRolloutChecklistArtifactId = Guid.NewGuid();
        var qaApprovalArtifactId = Guid.NewGuid();

        var editor = new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Three-agent process mock artifact handoff",
            Summary = "Small deterministic process that proves required artifact output and handoff behavior.",
            ValueStatement = "Validate process artifact contracts without running the full software-delivery process.",
            CustomerName = "Acme Customer",
            OwnerName = "Integration tests",
            GovernancePolicySummary = "Every automated step must persist each required process artifact before completion.",
            ChangeSummary = "Initial deterministic three-agent artifact handoff definition.",
            ConstitutionRuleSummary = "Downstream review must consume implementation artifacts rather than fabricating them.",
            OperatingModeSummary = "Assisted mock-agent execution with governed artifact handoff.",
            Status = ProcessDefinitionStatus.Draft,
            SimulationReadinessSummary = "Safe for integration validation without real LLM calls.",
            Roles =
            [
                CreateRole(
                    productOwnerRoleId,
                    ProcessMockAgentRoleKeys.ProductOwner,
                    "Product Owner",
                    "Own mock scope.",
                    "Process mock product owner for deterministic scope generation."),
                CreateRole(
                    developerRoleId,
                    ProcessMockAgentRoleKeys.Developer,
                    "Developer",
                    "Own implementation artifacts.",
                    "Process mock developer for deterministic implementation artifacts."),
                CreateRole(
                    qaRoleId,
                    ProcessMockAgentRoleKeys.Qa,
                    "QA Reviewer",
                    "Own artifact handoff review.",
                    "Process mock QA reviewer for deterministic artifact handoff approval.")
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = scopeStepId,
                    Key = StepKeys.Scope,
                    Title = "Write mock scope",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Mock product request.",
                    OutputContractSummary = "Scope and acceptance criteria.",
                    EvidenceContractSummary = "Scope artifact persisted for downstream implementation.",
                    DecisionRightsSummary = "Product owner owns scope completion.",
                    ExceptionPolicySummary = "Block when scope cannot be captured.",
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
                    Id = implementationStepId,
                    Key = StepKeys.Implementation,
                    Title = "Implement mock change set and rollout checklist",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((scopeStepId, null)),
                    InputContractSummary = "Scope artifact.",
                    OutputContractSummary = "Implementation change set and DB-free rollout checklist.",
                    EvidenceContractSummary = "Both required implementation artifacts are persisted before review.",
                    DecisionRightsSummary = "Developer owns implementation artifact completion.",
                    ExceptionPolicySummary = "Block when required implementation artifacts are missing.",
                    TargetLeadHours = 2,
                    CanvasX = 440,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(developerRoleId)
                    ],
                    ArtifactInputs =
                    [
                        new ProcessStepArtifactInputEditorModel
                        {
                            ArtifactExpectationId = scopeArtifactId
                        }
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            implementationChangeSetArtifactId,
                            ProcessArtifactKind.Deliverable,
                            ArtifactTitles.ImplementationChangeSet,
                            "Must be linked to tests, migration notes, and touched-surface inventory."),
                        CreateArtifactExpectation(
                            migrationRolloutChecklistArtifactId,
                            ProcessArtifactKind.Checklist,
                            ArtifactTitles.MigrationRolloutChecklist,
                            "Must name data changes, operational preconditions, and rollback steps.")
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = reviewStepId,
                    Key = StepKeys.Review,
                    Title = "QA recheck sample artifact handoff",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((implementationStepId, null)),
                    InputContractSummary = "Implementation change set and rollout checklist.",
                    OutputContractSummary = "QA approval for artifact handoff.",
                    EvidenceContractSummary = "QA approval artifact persisted after reading implementation artifacts.",
                    DecisionRightsSummary = "QA owns approval.",
                    ExceptionPolicySummary = "Block when implementation artifacts are missing.",
                    TargetLeadHours = 1,
                    CanvasX = 740,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(qaRoleId, ProcessResponsibilityKind.Reviewer)
                    ],
                    ArtifactInputs =
                    [
                        new ProcessStepArtifactInputEditorModel
                        {
                            ArtifactExpectationId = implementationChangeSetArtifactId
                        },
                        new ProcessStepArtifactInputEditorModel
                        {
                            ArtifactExpectationId = migrationRolloutChecklistArtifactId
                        }
                    ],
                    ArtifactExpectations =
                    [
                        CreateArtifactExpectation(
                            qaApprovalArtifactId,
                            ProcessArtifactKind.Evidence,
                            ArtifactTitles.QaApproval,
                            "QA approval artifact must record approval for release after reviewing the implementation artifacts.")
                    ]
                }
            ]
        };

        var stepIdsByKey = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            [StepKeys.Scope] = scopeStepId,
            [StepKeys.Implementation] = implementationStepId,
            [StepKeys.Review] = reviewStepId
        };
        var artifactExpectationIdsByTitle = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            [ArtifactTitles.Scope] = scopeArtifactId,
            [ArtifactTitles.ImplementationChangeSet] = implementationChangeSetArtifactId,
            [ArtifactTitles.MigrationRolloutChecklist] = migrationRolloutChecklistArtifactId,
            [ArtifactTitles.QaApproval] = qaApprovalArtifactId
        };

        return new ThreeAgentArtifactHandoffProcessDefinitionFixture(
            editor,
            stepIdsByKey,
            artifactExpectationIdsByTitle);
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
            AllowedFutureUsageSummary = "Regression proof for deterministic process mock artifact handoff only."
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

internal sealed record ThreeAgentArtifactHandoffProcessDefinitionFixture(
    ProcessDefinitionEditorModel Editor,
    IReadOnlyDictionary<string, Guid> StepIdsByKey,
    IReadOnlyDictionary<string, Guid> ArtifactExpectationIdsByTitle)
{
    public Guid StepId(string stepKey)
    {
        return StepIdsByKey[stepKey];
    }

    public Guid ArtifactExpectationId(string artifactTitle)
    {
        return ArtifactExpectationIdsByTitle[artifactTitle];
    }
}
