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

public sealed class PromptArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid? CollectionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public PromptGalleryItemKind Kind { get; set; } = PromptGalleryItemKind.FullPrompt;

    public string Phase { get; set; } = string.Empty;

    public PromptArtifactStatus Status { get; set; } = PromptArtifactStatus.Draft;

    public string CurrentDraftText { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public int CurrentVersionNumber { get; set; }

    public PromptArtifactProvenance Provenance { get; set; } = PromptArtifactProvenance.User;

    public string? SourceKey { get; set; }

    public string? SourceCatalog { get; set; }

    public string? SourceGroupKey { get; set; }

    public string? SourceGroupName { get; set; }

    public string? SourceItemKind { get; set; }

    public int? SourceOrderIndex { get; set; }

    public string? SourceFingerprint { get; set; }

    public double? RecommendedTemperature { get; set; }

    public int? RecommendedMaxOutputTokens { get; set; }

    public double? RecommendedTopP { get; set; }

    public bool IsArchived { get; set; }

    public DateTimeOffset? ArchivedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class PromptVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PromptArtifactId { get; set; }

    public int VersionNumber { get; set; }

    public string Content { get; set; } = string.Empty;

    public string CreationReason { get; set; } = string.Empty;

    public string OutputFormat { get; set; } = "Markdown";

    public string? SourceBlueprintId { get; set; }

    public string TitleSnapshot { get; set; } = string.Empty;

    public string SummarySnapshot { get; set; } = string.Empty;

    public PromptGalleryItemKind KindSnapshot { get; set; } = PromptGalleryItemKind.FullPrompt;

    public double? RecommendedTemperatureSnapshot { get; set; }

    public int? RecommendedMaxOutputTokensSnapshot { get; set; }

    public double? RecommendedTopPSnapshot { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class PromptCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class PromptTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;
}

public sealed class PromptArtifactTag
{
    public Guid PromptArtifactId { get; set; }

    public Guid PromptTagId { get; set; }
}

public sealed class PromptSupportedProviderModel
{
    public Guid PromptArtifactId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ProviderKey { get; set; } = string.Empty;

    public string ModelKey { get; set; } = string.Empty;
}

public sealed class PromptSupportedConsumer
{
    public Guid PromptArtifactId { get; set; }

    public PromptGalleryConsumer Consumer { get; set; }
}

public sealed class PromptTemplateToken
{
    public Guid PromptArtifactId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;
}

public sealed class PromptCompatibilityWarningPreference
{
    public Guid PromptArtifactId { get; set; }

    public PromptGalleryConsumer Consumer { get; set; }

    public PromptCompatibilityIssueCode IssueCode { get; set; }

    public bool IsSuppressed { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class PromptUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PromptArtifactId { get; set; }

    public int? PromptVersionNumber { get; set; }

    public Guid? ProjectId { get; set; }

    public string Phase { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string RepositoryName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string CommitSha { get; set; } = string.Empty;

    public string CommitUrl { get; set; } = string.Empty;

    public string UsageNote { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
