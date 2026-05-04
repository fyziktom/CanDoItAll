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
