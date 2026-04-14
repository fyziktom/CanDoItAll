namespace CanDoItAll.Modules.Processes;

internal static class ProcessCanvasActionIds
{
    public const string CreateRoleBlank = "process-role.blank";
    public const string CreateRoleProductOwner = "process-role.product-owner";
    public const string CreateRoleDeliveryManager = "process-role.delivery-manager";
    public const string CreateRoleSolutionArchitect = "process-role.solution-architect";
    public const string CreateRoleSoftwareEngineer = "process-role.software-engineer";
    public const string CreateRoleQaLead = "process-role.qa-lead";
    public const string CreateRoleSecurityReviewer = "process-role.security-reviewer";

    public const string CreateStepIntake = "process-step.intake";
    public const string CreateStepDecision = "process-step.decision";
    public const string CreateStepArchitecture = "process-step.architecture";
    public const string CreateStepImplementation = "process-step.implementation";
    public const string CreateStepQa = "process-step.qa";
    public const string CreateStepSecurityReview = "process-step.security-review";
    public const string CreateStepReleaseApproval = "process-step.release-approval";
    public const string CreateStepDeployment = "process-step.deployment";
    public const string CreateStepRetrospective = "process-step.retrospective";

    public const string EditDefinitionStep = "process-definition.edit-step";
    public const string EditDefinitionRole = "process-definition.edit-role";
    public const string AddDependentStep = "process-definition.add-dependent-step";
    public const string AddBranchOutcome = "process-definition.add-branch-outcome";
    public const string AddRoleBinding = "process-definition.add-role-binding";
    public const string AddArtifactExpectation = "process-definition.add-artifact-expectation";
    public const string RemoveDefinitionStep = "process-definition.remove-step";
    public const string OpenDefinitionToolbox = "process-definition.open-toolbox";

    public const string RuntimeStart = "process-runtime.start";
    public const string RuntimeComplete = "process-runtime.complete";
    public const string RuntimeBlock = "process-runtime.block";
    public const string RuntimeApproval = "process-runtime.approval";
    public const string RuntimeRefuse = "process-runtime.refuse";
    public const string RuntimeFail = "process-runtime.fail";
    public const string RuntimeRecordArtifact = "process-runtime.record-artifact";
}

public sealed record ProcessCanvasToolboxGroup(
    string Key,
    string Title,
    string Summary,
    IReadOnlyList<ProcessCanvasToolboxAction> Actions);

public sealed record ProcessCanvasToolboxAction(
    string ActionId,
    string Label,
    string Summary,
    string Tone);

public sealed record ProcessCanvasRoleTemplate(
    string ActionId,
    string Label,
    string Summary,
    Func<int, ProcessRoleEditorModel> Factory);

public sealed record ProcessCanvasStepTemplate(
    string ActionId,
    string Label,
    string Summary,
    Func<int, Guid?, double, double, ProcessStepEditorModel> Factory);
