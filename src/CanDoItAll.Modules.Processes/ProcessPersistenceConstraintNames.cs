namespace CanDoItAll.Modules.Processes;

internal static class ProcessPersistenceConstraintNames
{
    public const string DefinitionSlugUniqueIndex = "IX_Processes_Definitions_Slug";

    public const string StepDependencyUnconditionalUniqueIndex = "UX_ProcessStepDeps_Unconditional";

    public const string StepDependencyConditionalUniqueIndex = "UX_ProcessStepDeps_Conditional";

    public const string VersionDraftPerDefinitionUniqueIndex = "UX_ProcessVersions_DraftPerDef";

    public const string VersionPublishedPerDefinitionUniqueIndex = "UX_ProcessVersions_PubPerDef";
}
