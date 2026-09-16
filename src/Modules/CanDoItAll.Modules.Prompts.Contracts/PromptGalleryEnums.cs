namespace CanDoItAll.Modules.Prompts;

public enum PromptArtifactStatus
{
    Draft,
    Final
}

public enum PromptGalleryItemKind
{
    FullPrompt,
    Part
}

public enum PromptArtifactProvenance
{
    User,
    PackagedComponentCatalog,
    LegacyFactoryMigration,
    WorkflowMigration,
    ExternalImport,
    WorkflowCreated
}

public enum PromptGalleryConsumer
{
    Workflow,
    AgentRuntime,
    Chat,
    ProjectWorkbench
}
