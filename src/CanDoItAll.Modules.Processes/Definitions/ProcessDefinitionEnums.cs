namespace CanDoItAll.Modules.Processes;

public enum ProcessDefinitionStatus
{
    Draft,
    Published,
    Archived
}

public enum ProcessVersionStatus
{
    Draft,
    Published,
    Superseded,
    Archived
}

public enum ProcessCriticality
{
    Low,
    Standard,
    High,
    MissionCritical
}

public enum ProcessAutonomyLevel
{
    Manual,
    Assisted,
    Guarded,
    Delegated
}

public enum ProcessStepKind
{
    Start = 0,
    Work = 1,
    Decision = 2,
    Approval = 3,
    Review = 4,
    Delivery = 5,
    End = 6,
    Subprocess = 7
}

public enum ProcessResponsibilityKind
{
    Responsible,
    Reviewer,
    Approver,
    Backup
}

public enum ProcessExecutorKind
{
    Human,
    AiAgent,
    Workflow
}

public static class ProcessExecutorKindNames
{
    public const string Human = "Human";
    public const string AiAgent = "AI agent";
    public const string Workflow = "Workflow";

    public static string ToPersistedName(ProcessExecutorKind executorKind)
    {
        return executorKind switch
        {
            ProcessExecutorKind.AiAgent => AiAgent,
            ProcessExecutorKind.Workflow => Workflow,
            _ => Human
        };
    }

    public static string Normalize(string? executorKind)
    {
        return ToPersistedName(Resolve(executorKind));
    }

    public static ProcessExecutorKind Resolve(string? executorKind)
    {
        if (string.IsNullOrWhiteSpace(executorKind))
        {
            return ProcessExecutorKind.Human;
        }

        var normalized = new string(executorKind
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized switch
        {
            "workflow" or "workflows" => ProcessExecutorKind.Workflow,
            "ai" or "aiagent" or "agent" or "artificialintelligence" => ProcessExecutorKind.AiAgent,
            "human" or "person" or "people" or "team" or "teammember" or "workforce" or "manual" => ProcessExecutorKind.Human,
            _ when normalized.Contains("workflow", StringComparison.Ordinal) => ProcessExecutorKind.Workflow,
            _ when normalized.Contains("ai", StringComparison.Ordinal) || normalized.Contains("agent", StringComparison.Ordinal) => ProcessExecutorKind.AiAgent,
            _ => ProcessExecutorKind.Human
        };
    }

    public static bool IsWorkflow(string? executorKind)
    {
        return Resolve(executorKind) == ProcessExecutorKind.Workflow;
    }

    public static bool IsAiAgent(string? executorKind)
    {
        return Resolve(executorKind) == ProcessExecutorKind.AiAgent;
    }
}

public enum ProcessArtifactKind
{
    Brief,
    Evidence,
    Decision,
    Deliverable,
    Transcript,
    Checklist,
    Prompt,
    Dataset,
    Other
}

public enum ProcessArtifactTrustRequirement
{
    None,
    ReviewRequired,
    HumanApproved,
    TrustedSource
}

public enum ProcessSensitivityLevel
{
    Public,
    Internal,
    Confidential,
    Restricted
}
