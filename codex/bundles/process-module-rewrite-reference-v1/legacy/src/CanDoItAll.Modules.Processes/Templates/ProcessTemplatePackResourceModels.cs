namespace CanDoItAll.Modules.Processes;

public sealed class ProcessTemplateRoleResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string StaffingIntent { get; set; } = string.Empty;

    public string SnapshotSummary { get; set; } = string.Empty;

    public string PreferredExecutorKind { get; set; } = string.Empty;

    public string PreferredProjectAssignmentRole { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public bool AllowsFallback { get; set; } = true;

    public bool RequiresExplicitApproval { get; set; }

    public int DefaultAllocationPercent { get; set; }

    public string RoleTemplateSourceKey { get; set; } = string.Empty;

    public string RoleTemplateSnapshotName { get; set; } = string.Empty;

    public string SeniorityBand { get; set; } = string.Empty;

    public int MinimumYearsInPrimaryDiscipline { get; set; }

    public int MinimumYearsInSoftwareDelivery { get; set; }

    public List<string> DomainTags { get; set; } = [];

    public List<string> KnowledgeRequirements { get; set; } = [];

    public List<string> ExperienceRequirements { get; set; } = [];

    public List<string> DecisionRights { get; set; } = [];

    public List<string> OwnedArtifacts { get; set; } = [];

    public List<string> CollaborationExpectations { get; set; } = [];

    public List<string> AntiPatterns { get; set; } = [];

    public List<string> FitnessEvidence { get; set; } = [];

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateArtifactResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ArtifactKind { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string OwnerRoleKey { get; set; } = string.Empty;

    public string DefaultTrustRequirement { get; set; } = string.Empty;

    public string DefaultSensitivityLevel { get; set; } = string.Empty;

    public int DefaultRetentionDays { get; set; }

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ValidationRequirementSummary { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateChecklistResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string OwnerRoleKey { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string EntryCriteria { get; set; } = string.Empty;

    public string ExitCriteria { get; set; } = string.Empty;

    public List<string> Checks { get; set; } = [];

    public List<string> EvidenceExpectations { get; set; } = [];

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplateValidationResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string OwnerRoleKey { get; set; } = string.Empty;

    public string Gate { get; set; } = string.Empty;

    public string FailureSeverity { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string PassCriteria { get; set; } = string.Empty;

    public string FailCriteria { get; set; } = string.Empty;

    public string EscalationRule { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}

public sealed class ProcessTemplatePromptResource
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string AudienceRoleKey { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> RequiredInputs { get; set; } = [];

    public List<string> OutputSchema { get; set; } = [];

    public List<string> RefusalConditions { get; set; } = [];

    public string Scope { get; set; } = string.Empty;

    public string ProcessKey { get; set; } = string.Empty;

    public string DocPath { get; set; } = string.Empty;
}
