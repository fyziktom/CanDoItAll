namespace CanDoItAll.Modules.Prompts;

/// <summary>
/// Status of a Prompt Gallery item, as a JSON integer: 0 Draft (the item was last saved as a draft; set by every draft
/// save), 1 Final (an immutable version was created from the current draft; set by version creation and by imports).
/// Only Final items that are not archived are copied into the search projection.
/// </summary>
public enum PromptArtifactStatus
{
    Draft,
    Final
}

/// <summary>
/// Kind of Prompt Gallery item, as a JSON integer: 0 FullPrompt (a complete prompt), 1 Part (a reusable part to combine
/// with other prompt content).
/// </summary>
public enum PromptGalleryItemKind
{
    FullPrompt,
    Part
}

/// <summary>
/// Origin of a Prompt Gallery item, as a JSON integer: 0 User (created through the Gallery or this API),
/// 1 PackagedComponentCatalog (imported from the prompt component catalog shipped with the product),
/// 2 LegacyFactoryMigration, 3 WorkflowMigration, 4 ExternalImport, 5 WorkflowCreated (items imported into the Gallery
/// by other parts of the product).
/// </summary>
public enum PromptArtifactProvenance
{
    User,
    PackagedComponentCatalog,
    LegacyFactoryMigration,
    WorkflowMigration,
    ExternalImport,
    WorkflowCreated
}

/// <summary>
/// Part of the product that uses Prompt Gallery items, as a JSON integer: 0 Workflow, 1 AgentRuntime, 2 Chat,
/// 3 ProjectWorkbench. An item that declares no supported consumers can be used by all of them.
/// </summary>
public enum PromptGalleryConsumer
{
    Workflow,
    AgentRuntime,
    Chat,
    ProjectWorkbench
}
