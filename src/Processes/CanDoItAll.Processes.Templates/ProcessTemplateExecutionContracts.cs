namespace CanDoItAll.Processes.Templates;

public static class ProcessTemplateStepExecutionClasses
{
    public const string AgentReasoningOnly = "AgentReasoningOnly";
    public const string AgentWithToolPlanGuard = "AgentWithToolPlanGuard";
    public const string DeterministicToolPlan = "DeterministicToolPlan";
    public const string RuntimeOwnedToolPlan = "RuntimeOwnedToolPlan";
    public const string RuntimeOwnedSubprocess = "RuntimeOwnedSubprocess";
    public const string BranchDecision = "BranchDecision";

    public static bool IsKnown(string? executionClass)
        => Normalize(executionClass) is AgentReasoningOnly or
            AgentWithToolPlanGuard or
            DeterministicToolPlan or
            RuntimeOwnedToolPlan or
            RuntimeOwnedSubprocess or
            BranchDecision;

    public static bool RequiresDeterministicToolPlan(string? executionClass)
        => Normalize(executionClass) is AgentWithToolPlanGuard or DeterministicToolPlan or RuntimeOwnedToolPlan;

    public static bool IsRuntimeOwnedToolPlan(string? executionClass)
        => string.Equals(Normalize(executionClass), RuntimeOwnedToolPlan, StringComparison.Ordinal);

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

    public string RuntimeOwnedExecutorKey { get; set; } = string.Empty;

    public ProcessTemplateDeterministicToolPlanDocument? DeterministicToolPlan { get; set; }

    public List<ProcessTemplateRequiredReceiptDocument> RequiredReceipts { get; set; } = [];

    public List<ProcessTemplateProducedArtifactSlotDocument> ProducedArtifactSlots { get; set; } = [];

    public List<string> RequiredRuntimeToolNames { get; set; } = [];

    public List<string> RequiredHostCapabilities { get; set; } = [];
}

public sealed class ProcessTemplateDriverActivationDocument
{
    public string DriverKey { get; set; } = string.Empty;

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<ProcessTemplateDriverArtifactBindingDocument> InputArtifactBindings { get; set; } = [];
}

public sealed class ProcessTemplateDriverArtifactBindingDocument
{
    public string BindingKey { get; set; } = string.Empty;

    public string SourceStepKey { get; set; } = string.Empty;

    public string ArtifactExpectationKey { get; set; } = string.Empty;

    public string PayloadSchema { get; set; } = string.Empty;
}

public sealed class ProcessTemplateStepCompletionPolicyDocument
{
    public List<ProcessTemplateProductToolReceiptRequirementDocument> RequiredProductToolReceipts { get; set; } = [];

    public List<ProcessTemplateCompletionIssueRouteDocument> CompletionIssueRoutes { get; set; } = [];

    public List<string> AcceptanceCriteriaRequiredBranchOutcomeKeys { get; set; } = [];

    public bool RequiresProductSourceInspection { get; set; }

    public List<string> ProductSourceInspectionRequiredBranchOutcomeKeys { get; set; } = [];

    public List<string> ProductMutationRequiredBranchOutcomeKeys { get; set; } = [];

    public bool RequiresProductMutationBeforeManagedOutput { get; set; }

    public List<string> ProductMutationToolNames { get; set; } = [];

    public List<string> RuntimeRoutedBranchOutcomeKeys { get; set; } = [];
}

public sealed class ProcessTemplateProductToolReceiptRequirementDocument
{
    public string Key { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public List<string> EnforceBranchOutcomeKeys { get; set; } = [];

    public string Reason { get; set; } = string.Empty;

    public bool AllowFailedExecutionReceipt { get; set; }
}

public sealed class ProcessTemplateCompletionIssueRouteDocument
{
    public string IssueCode { get; set; } = string.Empty;

    public List<string> SourceBranchOutcomeKeys { get; set; } = [];

    public string TargetBranchOutcomeKey { get; set; } = string.Empty;

    public string TargetBranchOutcomeTitle { get; set; } = string.Empty;

    public bool RequiresDefectEvidence { get; set; }

    public bool OnlyAfterAutomaticRetry { get; set; }
}

public sealed class ProcessTemplateDeterministicToolPlanDocument
{
    public string PlanKey { get; set; } = string.Empty;

    public string PlanKind { get; set; } = string.Empty;

    public string ScriptRef { get; set; } = string.Empty;

    public string ScriptRefLaunchVariable { get; set; } = string.Empty;

    public string ScriptLaunchVariable { get; set; } = string.Empty;

    public string SideEffectManifestLaunchVariable { get; set; } = string.Empty;

    public string ExecutionPlanLaunchVariable { get; set; } = string.Empty;

    public bool RequiresReadbackChecks { get; set; }

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

    public string FailureReconciliationPolicyKey { get; set; } = string.Empty;
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
