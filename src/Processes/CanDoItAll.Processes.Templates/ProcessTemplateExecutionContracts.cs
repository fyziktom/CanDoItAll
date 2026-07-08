namespace CanDoItAll.Processes.Templates;

public static class ProcessTemplateStepExecutionClasses
{
    public const string AgentReasoningOnly = "AgentReasoningOnly";
    public const string AgentWithToolPlanGuard = "AgentWithToolPlanGuard";
    public const string DeterministicToolPlan = "DeterministicToolPlan";
    public const string RuntimeOwnedSubprocess = "RuntimeOwnedSubprocess";
    public const string BranchDecision = "BranchDecision";

    public static bool IsKnown(string? executionClass)
        => Normalize(executionClass) is AgentReasoningOnly or
            AgentWithToolPlanGuard or
            DeterministicToolPlan or
            RuntimeOwnedSubprocess or
            BranchDecision;

    public static bool RequiresDeterministicToolPlan(string? executionClass)
        => Normalize(executionClass) is AgentWithToolPlanGuard or DeterministicToolPlan;

    public static bool IsRuntimeOwnedSubprocess(string? executionClass)
        => string.Equals(Normalize(executionClass), RuntimeOwnedSubprocess, StringComparison.Ordinal);

    public static bool IsBranchDecision(string? executionClass)
        => string.Equals(Normalize(executionClass), BranchDecision, StringComparison.Ordinal);

    public static string Normalize(string? executionClass)
        => string.IsNullOrWhiteSpace(executionClass) ? string.Empty : executionClass.Trim();
}

public sealed class ProcessTemplateStepExecutionContractDocument
{
    public string ExecutionClass { get; set; } = string.Empty;

    public ProcessTemplateDeterministicToolPlanDocument? DeterministicToolPlan { get; set; }

    public List<ProcessTemplateRequiredReceiptDocument> RequiredReceipts { get; set; } = [];

    public List<ProcessTemplateProducedArtifactSlotDocument> ProducedArtifactSlots { get; set; } = [];

    public List<string> RequiredRuntimeToolNames { get; set; } = [];
}

public sealed class ProcessTemplateDeterministicToolPlanDocument
{
    public string PlanKey { get; set; } = string.Empty;

    public string PlanKind { get; set; } = string.Empty;

    public string ScriptRef { get; set; } = string.Empty;

    public string ScriptRefLaunchVariable { get; set; } = string.Empty;

    public string ScriptLaunchVariable { get; set; } = string.Empty;

    public string SideEffectManifestLaunchVariable { get; set; } = string.Empty;

    public List<ProcessTemplateToolPlanOperationDocument> Operations { get; set; } = [];

    public List<ProcessTemplateRequiredReceiptDocument> RequiredReceipts { get; set; } = [];

    public List<ProcessTemplateFileContentCheckDocument> ReadbackChecks { get; set; } = [];
}

public sealed class ProcessTemplateToolPlanOperationDocument
{
    public string Key { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string RequiredReceiptKey { get; set; } = string.Empty;

    public string ArgumentsSummary { get; set; } = string.Empty;

    public string IdempotencyPolicyKey { get; set; } = string.Empty;
}

public sealed class ProcessTemplateRequiredReceiptDocument
{
    public string Key { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string Predicate { get; set; } = string.Empty;

    public bool RequireSuccessfulExit { get; set; } = true;

    public bool RequireCurrentRun { get; set; } = true;
}

public sealed class ProcessTemplateProducedArtifactSlotDocument
{
    public string ArtifactExpectationKey { get; set; } = string.Empty;

    public string MaterializationMode { get; set; } = string.Empty;
}

public sealed class ProcessTemplateFileContentCheckDocument
{
    public List<string> PathCandidates { get; set; } = [];

    public List<List<string>> RequiredTextAnyGroups { get; set; } = [];
}
