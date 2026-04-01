using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activity_Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ArtifactKind = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Route = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity_Entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    BlockKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    IsRecommendedByDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    PromptTypeRules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    BlueprintRules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    PhaseRules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    GroupKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StackTagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TemplateTokensJson = table.Column<string>(type: "TEXT", nullable: false),
                    ToolboxEligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogSource = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBlueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PromptType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Guidance = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedFlowTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecommendedFlowKey = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    RecommendedBlockKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogSource = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBlueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBuildSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    BlueprintId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FlowTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PromptArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PromptRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelectedPromptRunNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RepositoryName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SelectedBlockIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedResourceIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    WarningSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CanvasUiStateJson = table.Column<string>(type: "TEXT", nullable: false),
                    ComponentCustomizationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SessionAttachmentsJson = table.Column<string>(type: "TEXT", nullable: false),
                    WizardStepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    HasCustomizedBlocks = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBuildSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptFlowTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    BlockIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    BlockKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    AgentSequenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    PromptTypeRules = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogSource = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptFlowTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptRunNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptBlockDefinitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PromptArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentPromptRunNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BranchKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    BranchLabel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptRunNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlowTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Infrastructure_BackgroundJobRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infrastructure_BackgroundJobRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Infrastructure_SearchDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infrastructure_SearchDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectHierarchyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChildProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_ProjectHierarchyLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectOptionSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    OptionName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_ProjectOptionSelections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectPhases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Goal = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_ProjectPhases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Objective = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPhase = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentDraftText = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptArtifactTags",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptTagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptArtifactTags", x => new { x.PromptArtifactId, x.PromptTagId });
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptVersionNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CommitUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UsageNote = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreationReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OutputFormat = table.Column<string>(type: "TEXT", nullable: false),
                    SourceBlueprintId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resources_ProjectResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    LocationOrIdentifier = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    LinkedSecretIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Sensitivity = table.Column<int>(type: "INTEGER", nullable: false),
                    SupportsPreview = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsIndexing = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources_ProjectResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Security_SecretRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    EncryptedPayload = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    RotationNote = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Security_SecretRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Security_SecretReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SecretRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContextType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ContextId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Security_SecretReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StoryOrFeature = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ArtifactPath = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    EvidenceKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CoverageGoal = table.Column<string>(type: "TEXT", nullable: false),
                    PlaywrightSpecPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Runner = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Findings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidationType = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionLabel = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Checklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ValidationType = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtifactTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ArtifactRoute = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceContent = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectObjectLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceNodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TargetNodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    LinkKind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystemManaged = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectObjectLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ObjectType = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 800, nullable: false),
                    ExternalArtifactKind = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ExternalArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ObjectSubtype = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MediaRelativePath = table.Column<string>(type: "TEXT", maxLength: 800, nullable: false),
                    MediaContentType = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    MediaOriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ProgressMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProgressPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    MarkerIcon = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MarkerTone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    MarkerLabel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParentNodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    PositionX = table.Column<double>(type: "REAL", nullable: false),
                    PositionY = table.Column<double>(type: "REAL", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsSystemManaged = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectObjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectStructureLeases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    LeaseToken = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RepositoryRoot = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RenewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectStructureLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectStructureOperationAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: true),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    AgentId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RepositoryRoot = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    RequestSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectStructureOperationAnalytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ViewStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SurfaceKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ViewStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProjectStructureAgentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenCipherText = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapabilityMask = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoApproveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalRequiredMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    RequireApprovalForAllMutations = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProjectStructureAgentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProjectStructureAgentProjectOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapabilityMask = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoApproveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalRequiredMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    RequireApprovalForAllMutations = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProjectStructureAgentProjectOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProjectStructureAgentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CentralBaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    InstallScriptPath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SetupReadmePath = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    DefaultAutoApproveMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultApprovalRequiredMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProjectStructureAgentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProviderProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProviderKind = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ApiKeySecretId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultModel = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsToolCalling = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsStructuredOutput = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsVision = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastHealthCheckAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastHealthStatus = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ExtraSettingsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProviderProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DefaultProviderProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultPromptOutputFormat = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_Settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activity_Entries_CreatedAtUtc",
                table: "Activity_Entries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Infrastructure_SearchDocuments_SourceType_SourceKey",
                table: "Infrastructure_SearchDocuments",
                columns: new[] { "SourceType", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectHierarchyLinks_ChildProjectId",
                table: "Projects_ProjectHierarchyLinks",
                column: "ChildProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectHierarchyLinks_ParentProjectId",
                table: "Projects_ProjectHierarchyLinks",
                column: "ParentProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectHierarchyLinks_ParentProjectId_ChildProjectId",
                table: "Projects_ProjectHierarchyLinks",
                columns: new[] { "ParentProjectId", "ChildProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectOptionSelections_ProjectId_Category",
                table: "Projects_ProjectOptionSelections",
                columns: new[] { "ProjectId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectPhases_ProjectId_OrderIndex",
                table: "Projects_ProjectPhases",
                columns: new[] { "ProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptTags_Name",
                table: "Prompts_PromptTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptVersions_PromptArtifactId_VersionNumber",
                table: "Prompts_PromptVersions",
                columns: new[] { "PromptArtifactId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Security_SecretReferences_ContextType_ContextId",
                table: "Security_SecretReferences",
                columns: new[] { "ContextType", "ContextId" });

            migrationBuilder.CreateIndex(
                name: "IX_Validation_Runs_CreatedAtUtc",
                table: "Validation_Runs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjectLinks_ProjectId_SourceNodeKey_TargetNodeKey_LinkKind_IsSystemManaged",
                table: "Workbench_ProjectObjectLinks",
                columns: new[] { "ProjectId", "SourceNodeKey", "TargetNodeKey", "LinkKind", "IsSystemManaged" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_NodeKey",
                table: "Workbench_ProjectObjects",
                columns: new[] { "ProjectId", "NodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureLeases_LeaseToken",
                table: "Workbench_ProjectStructureLeases",
                column: "LeaseToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureLeases_ScopeKind_ScopeKey",
                table: "Workbench_ProjectStructureLeases",
                columns: new[] { "ScopeKind", "ScopeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureOperationAnalytics_OccurredAtUtc",
                table: "Workbench_ProjectStructureOperationAnalytics",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureOperationAnalytics_ProjectId_OperationName",
                table: "Workbench_ProjectStructureOperationAnalytics",
                columns: new[] { "ProjectId", "OperationName" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ViewStates_ProjectId_SurfaceKind",
                table: "Workbench_ViewStates",
                columns: new[] { "ProjectId", "SurfaceKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ProjectStructureAgentProjectOverrides_ProfileId_ProjectId",
                table: "Workspace_ProjectStructureAgentProjectOverrides",
                columns: new[] { "ProfileId", "ProjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activity_Entries");

            migrationBuilder.DropTable(
                name: "Factory_PromptBlocks");

            migrationBuilder.DropTable(
                name: "Factory_PromptBlueprints");

            migrationBuilder.DropTable(
                name: "Factory_PromptBuildSessions");

            migrationBuilder.DropTable(
                name: "Factory_PromptFlowTemplates");

            migrationBuilder.DropTable(
                name: "Factory_PromptRunNodes");

            migrationBuilder.DropTable(
                name: "Factory_PromptRuns");

            migrationBuilder.DropTable(
                name: "Infrastructure_BackgroundJobRecords");

            migrationBuilder.DropTable(
                name: "Infrastructure_SearchDocuments");

            migrationBuilder.DropTable(
                name: "Projects_ProjectHierarchyLinks");

            migrationBuilder.DropTable(
                name: "Projects_ProjectOptionSelections");

            migrationBuilder.DropTable(
                name: "Projects_ProjectPhases");

            migrationBuilder.DropTable(
                name: "Projects_Projects");

            migrationBuilder.DropTable(
                name: "Prompts_PromptArtifacts");

            migrationBuilder.DropTable(
                name: "Prompts_PromptArtifactTags");

            migrationBuilder.DropTable(
                name: "Prompts_PromptCollections");

            migrationBuilder.DropTable(
                name: "Prompts_PromptTags");

            migrationBuilder.DropTable(
                name: "Prompts_PromptUsageRecords");

            migrationBuilder.DropTable(
                name: "Prompts_PromptVersions");

            migrationBuilder.DropTable(
                name: "Resources_ProjectResources");

            migrationBuilder.DropTable(
                name: "Security_SecretRecords");

            migrationBuilder.DropTable(
                name: "Security_SecretReferences");

            migrationBuilder.DropTable(
                name: "TestLab_TestCases");

            migrationBuilder.DropTable(
                name: "TestLab_TestEvidence");

            migrationBuilder.DropTable(
                name: "TestLab_TestPlans");

            migrationBuilder.DropTable(
                name: "TestLab_TestRuns");

            migrationBuilder.DropTable(
                name: "Validation_Findings");

            migrationBuilder.DropTable(
                name: "Validation_Checklists");

            migrationBuilder.DropTable(
                name: "Validation_Runs");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectObjectLinks");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectObjects");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectStructureLeases");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectStructureOperationAnalytics");

            migrationBuilder.DropTable(
                name: "Workbench_ViewStates");

            migrationBuilder.DropTable(
                name: "Workspace_ProjectStructureAgentProfiles");

            migrationBuilder.DropTable(
                name: "Workspace_ProjectStructureAgentProjectOverrides");

            migrationBuilder.DropTable(
                name: "Workspace_ProjectStructureAgentSettings");

            migrationBuilder.DropTable(
                name: "Workspace_ProviderProfiles");

            migrationBuilder.DropTable(
                name: "Workspace_Settings");
        }
    }
}
