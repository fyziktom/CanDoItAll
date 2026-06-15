namespace CanDoItAll.Modules.Processes;

internal static class ProcessPersistenceConstraintNames
{
    public const string DefinitionSlugUniqueIndex = "IX_Processes_Definitions_Slug";

    public const string DefinitionMessagingPolicyUniqueIndex = "UX_ProcessRoleMessagingPolicies_SourceTarget";

    public const string StepDependencyUnconditionalUniqueIndex = "UX_ProcessStepDeps_Unconditional";

    public const string StepDependencyConditionalUniqueIndex = "UX_ProcessStepDeps_Conditional";

    public const string StepRunPerDefinitionUniqueIndex = "UX_ProcessStepRuns_RunStep";

    public const string SubprocessRunPerParentStepUniqueIndex = "UX_ProcessRuns_ParentStepRun";

    public const string RunAssignmentRunScopedUniqueIndex = "UX_ProcessRunAssignments_RunScoped";

    public const string RunAssignmentStepScopedUniqueIndex = "UX_ProcessRunAssignments_StepScoped";

    public const string LaunchPlanRoleUniqueIndex = "UX_ProcessLaunchPlanRoles_Role";

    public const string LaunchPlanProvisioningRoleUniqueIndex = "UX_ProcessLaunchProvisioning_Role";

    public const string VersionDraftPerDefinitionUniqueIndex = "UX_ProcessVersions_DraftPerDef";

    public const string VersionPublishedPerDefinitionUniqueIndex = "UX_ProcessVersions_PubPerDef";
}
