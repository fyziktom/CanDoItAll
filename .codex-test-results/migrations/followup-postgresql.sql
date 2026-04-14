CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Activity_Entries" (
    "Id" uuid NOT NULL,
    "Category" character varying(80) NOT NULL,
    "Action" character varying(80) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "ProjectId" uuid,
    "ArtifactKind" character varying(120),
    "ArtifactId" uuid,
    "Route" character varying(500),
    "Actor" character varying(120) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Activity_Entries" PRIMARY KEY ("Id")
);

CREATE TABLE "Factory_PromptBlocks" (
    "Id" uuid NOT NULL,
    "Key" character varying(180) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "BlockKind" integer NOT NULL,
    "Summary" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "IsRecommendedByDefault" boolean NOT NULL,
    "PromptTypeRules" character varying(300) NOT NULL,
    "BlueprintRules" character varying(300) NOT NULL,
    "PhaseRules" character varying(300) NOT NULL,
    "GroupKey" character varying(120) NOT NULL,
    "TagsJson" TEXT NOT NULL,
    "StackTagsJson" TEXT NOT NULL,
    "TemplateTokensJson" TEXT NOT NULL,
    "ToolboxEligible" boolean NOT NULL,
    "OrderIndex" integer NOT NULL,
    "CatalogSource" character varying(80) NOT NULL,
    CONSTRAINT "PK_Factory_PromptBlocks" PRIMARY KEY ("Id")
);

CREATE TABLE "Factory_PromptBlueprints" (
    "Id" uuid NOT NULL,
    "Key" character varying(180) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "PromptType" character varying(80) NOT NULL,
    "Summary" TEXT NOT NULL,
    "Guidance" TEXT NOT NULL,
    "RecommendedFlowTemplateId" uuid,
    "RecommendedFlowKey" character varying(180) NOT NULL,
    "RecommendedBlockKeysJson" TEXT NOT NULL,
    "OrderIndex" integer NOT NULL,
    "CatalogSource" character varying(80) NOT NULL,
    CONSTRAINT "PK_Factory_PromptBlueprints" PRIMARY KEY ("Id")
);

CREATE TABLE "Factory_PromptBuildSessions" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ProjectId" uuid,
    "Phase" character varying(120) NOT NULL,
    "BlueprintId" uuid,
    "FlowTemplateId" uuid,
    "ProviderProfileId" uuid,
    "PromptArtifactId" uuid,
    "PromptRunId" uuid,
    "SelectedPromptRunNodeId" uuid,
    "RepositoryName" character varying(200) NOT NULL,
    "BranchName" character varying(120) NOT NULL,
    "CommitSha" character varying(80) NOT NULL,
    "SelectedBlockIdsJson" TEXT NOT NULL,
    "SelectedResourceIdsJson" TEXT NOT NULL,
    "GeneratedPrompt" TEXT NOT NULL,
    "WarningSummary" TEXT NOT NULL,
    "CanvasUiStateJson" TEXT NOT NULL,
    "ComponentCustomizationsJson" TEXT NOT NULL,
    "SessionAttachmentsJson" TEXT NOT NULL,
    "WizardStepIndex" integer NOT NULL,
    "HasCustomizedBlocks" boolean NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Factory_PromptBuildSessions" PRIMARY KEY ("Id")
);

CREATE TABLE "Factory_PromptFlowTemplates" (
    "Id" uuid NOT NULL,
    "Key" character varying(180) NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Summary" TEXT NOT NULL,
    "BlockIdsJson" TEXT NOT NULL,
    "BlockKeysJson" TEXT NOT NULL,
    "AgentSequenceJson" TEXT NOT NULL,
    "PromptTypeRules" character varying(300) NOT NULL,
    "OrderIndex" integer NOT NULL,
    "CatalogSource" character varying(80) NOT NULL,
    CONSTRAINT "PK_Factory_PromptFlowTemplates" PRIMARY KEY ("Id")
);

CREATE TABLE "Factory_PromptRunNodes" (
    "Id" uuid NOT NULL,
    "PromptRunId" uuid NOT NULL,
    "PromptBlockDefinitionId" uuid,
    "PromptArtifactId" uuid,
    "ParentPromptRunNodeId" uuid,
    "Title" character varying(200) NOT NULL,
    "BranchKey" character varying(80) NOT NULL,
    "BranchLabel" character varying(120) NOT NULL,
    "Sequence" integer NOT NULL,
    "State" integer NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_Factory_PromptRunNodes" PRIMARY KEY ("Id")
);

CREATE TABLE "Factory_PromptRuns" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "FlowTemplateId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Phase" character varying(120) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Factory_PromptRuns" PRIMARY KEY ("Id")
);

CREATE TABLE "Infrastructure_BackgroundJobRecords" (
    "Id" uuid NOT NULL,
    "JobType" character varying(120) NOT NULL,
    "Description" character varying(300) NOT NULL,
    "State" character varying(40) NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "ErrorSummary" TEXT,
    "CorrelationId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Infrastructure_BackgroundJobRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "Infrastructure_SearchDocuments" (
    "Id" uuid NOT NULL,
    "SourceType" character varying(120) NOT NULL,
    "SourceKey" character varying(200) NOT NULL,
    "ProjectId" uuid,
    "Category" character varying(120) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Summary" TEXT NOT NULL,
    "Body" TEXT NOT NULL,
    "Route" character varying(500) NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Infrastructure_SearchDocuments" PRIMARY KEY ("Id")
);

CREATE TABLE "Projects_ProjectHierarchyLinks" (
    "Id" uuid NOT NULL,
    "ParentProjectId" uuid NOT NULL,
    "ChildProjectId" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Projects_ProjectHierarchyLinks" PRIMARY KEY ("Id")
);

CREATE TABLE "Projects_ProjectOptionSelections" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "Category" integer NOT NULL,
    "OptionName" character varying(200) NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_Projects_ProjectOptionSelections" PRIMARY KEY ("Id")
);

CREATE TABLE "Projects_ProjectPhases" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Goal" TEXT NOT NULL,
    "Status" integer NOT NULL,
    "OrderIndex" integer NOT NULL,
    "StartDateUtc" timestamp with time zone,
    "EndDateUtc" timestamp with time zone,
    CONSTRAINT "PK_Projects_ProjectPhases" PRIMARY KEY ("Id")
);

CREATE TABLE "Projects_Projects" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Slug" character varying(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "Objective" TEXT NOT NULL,
    "Status" integer NOT NULL,
    "CurrentPhase" character varying(120) NOT NULL,
    "TargetDateUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Projects_Projects" PRIMARY KEY ("Id")
);

CREATE TABLE "Prompts_PromptArtifacts" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid,
    "CollectionId" uuid,
    "Title" character varying(200) NOT NULL,
    "Phase" character varying(80) NOT NULL,
    "Status" integer NOT NULL,
    "CurrentDraftText" TEXT NOT NULL,
    "CurrentVersionNumber" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Prompts_PromptArtifacts" PRIMARY KEY ("Id")
);

CREATE TABLE "Prompts_PromptArtifactTags" (
    "PromptArtifactId" uuid NOT NULL,
    "PromptTagId" uuid NOT NULL,
    CONSTRAINT "PK_Prompts_PromptArtifactTags" PRIMARY KEY ("PromptArtifactId", "PromptTagId")
);

CREATE TABLE "Prompts_PromptCollections" (
    "Id" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    "Description" TEXT NOT NULL,
    CONSTRAINT "PK_Prompts_PromptCollections" PRIMARY KEY ("Id")
);

CREATE TABLE "Prompts_PromptTags" (
    "Id" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    CONSTRAINT "PK_Prompts_PromptTags" PRIMARY KEY ("Id")
);

CREATE TABLE "Prompts_PromptUsageRecords" (
    "Id" uuid NOT NULL,
    "PromptArtifactId" uuid NOT NULL,
    "PromptVersionNumber" integer,
    "ProjectId" uuid,
    "Phase" text NOT NULL,
    "ProviderName" character varying(120) NOT NULL,
    "RepositoryName" character varying(200) NOT NULL,
    "BranchName" character varying(120) NOT NULL,
    "CommitSha" character varying(80) NOT NULL,
    "CommitUrl" character varying(500) NOT NULL,
    "UsageNote" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Prompts_PromptUsageRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "Prompts_PromptVersions" (
    "Id" uuid NOT NULL,
    "PromptArtifactId" uuid NOT NULL,
    "VersionNumber" integer NOT NULL,
    "Content" TEXT NOT NULL,
    "CreationReason" character varying(200) NOT NULL,
    "OutputFormat" text NOT NULL,
    "SourceBlueprintId" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Prompts_PromptVersions" PRIMARY KEY ("Id")
);

CREATE TABLE "Resources_ProjectResources" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "ResourceKind" integer NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "LocationOrIdentifier" character varying(1000) NOT NULL,
    "ConfigJson" TEXT NOT NULL,
    "LinkedSecretIdsJson" TEXT NOT NULL,
    "ValidationStatus" integer NOT NULL,
    "Sensitivity" integer NOT NULL,
    "SupportsPreview" boolean NOT NULL,
    "SupportsIndexing" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Resources_ProjectResources" PRIMARY KEY ("Id")
);

CREATE TABLE "Security_SecretRecords" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Kind" integer NOT NULL,
    "EncryptedPayload" TEXT NOT NULL,
    "Scope" character varying(50) NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "RotationNote" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Security_SecretRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "Security_SecretReferences" (
    "Id" uuid NOT NULL,
    "SecretRecordId" uuid NOT NULL,
    "ContextType" character varying(80) NOT NULL,
    "ContextId" character varying(120) NOT NULL,
    "Purpose" character varying(120) NOT NULL,
    CONSTRAINT "PK_Security_SecretReferences" PRIMARY KEY ("Id")
);

CREATE TABLE "TestLab_TestCases" (
    "Id" uuid NOT NULL,
    "TestPlanId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "StoryOrFeature" character varying(200) NOT NULL,
    "Status" integer NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_TestLab_TestCases" PRIMARY KEY ("Id")
);

CREATE TABLE "TestLab_TestEvidence" (
    "Id" uuid NOT NULL,
    "TestPlanId" uuid NOT NULL,
    "EvidenceLabel" character varying(200) NOT NULL,
    "ArtifactPath" character varying(600) NOT NULL,
    "EvidenceKind" character varying(80) NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_TestLab_TestEvidence" PRIMARY KEY ("Id")
);

CREATE TABLE "TestLab_TestPlans" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid,
    "Title" character varying(200) NOT NULL,
    "Phase" character varying(120) NOT NULL,
    "CoverageGoal" TEXT NOT NULL,
    "PlaywrightSpecPath" character varying(500) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TestLab_TestPlans" PRIMARY KEY ("Id")
);

CREATE TABLE "TestLab_TestRuns" (
    "Id" uuid NOT NULL,
    "TestPlanId" uuid NOT NULL,
    "ExecutedAtUtc" timestamp with time zone NOT NULL,
    "Runner" character varying(120) NOT NULL,
    "Result" integer NOT NULL,
    "Summary" TEXT NOT NULL,
    CONSTRAINT "PK_TestLab_TestRuns" PRIMARY KEY ("Id")
);

CREATE TABLE "Validation_Findings" (
    "Id" uuid NOT NULL,
    "ValidationRunId" uuid NOT NULL,
    "RuleCode" character varying(120) NOT NULL,
    "Severity" integer NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Detail" TEXT NOT NULL,
    "RecommendedAction" TEXT NOT NULL,
    CONSTRAINT "PK_Validation_Findings" PRIMARY KEY ("Id")
);

CREATE TABLE "Validation_Checklists" (
    "Id" uuid NOT NULL,
    "ValidationType" integer NOT NULL,
    "VersionLabel" character varying(40) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ItemsJson" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Validation_Checklists" PRIMARY KEY ("Id")
);

CREATE TABLE "Validation_Runs" (
    "Id" uuid NOT NULL,
    "ChecklistId" uuid NOT NULL,
    "ProjectId" uuid,
    "ValidationType" integer NOT NULL,
    "ArtifactTitle" character varying(200) NOT NULL,
    "ArtifactRoute" character varying(500) NOT NULL,
    "SourceContent" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Decision" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Validation_Runs" PRIMARY KEY ("Id")
);

CREATE TABLE "Workbench_ProjectObjectLinks" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "SourceNodeKey" character varying(160) NOT NULL,
    "TargetNodeKey" character varying(160) NOT NULL,
    "LinkKind" integer NOT NULL,
    "IsSystemManaged" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectObjectLinks" PRIMARY KEY ("Id")
);

CREATE TABLE "Workbench_ProjectObjects" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "NodeKey" character varying(160) NOT NULL,
    "ObjectType" integer NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Subtitle" character varying(240) NOT NULL,
    "Status" character varying(120) NOT NULL,
    "Notes" TEXT NOT NULL,
    "Route" character varying(800) NOT NULL,
    "ExternalArtifactKind" character varying(120) NOT NULL,
    "ExternalArtifactId" uuid,
    "ObjectSubtype" character varying(120) NOT NULL,
    "MediaRelativePath" character varying(800) NOT NULL,
    "MediaContentType" character varying(160) NOT NULL,
    "MediaOriginalFileName" character varying(260) NOT NULL,
    "ProgressMode" character varying(32) NOT NULL,
    "ProgressPercent" integer NOT NULL,
    "MarkerIcon" character varying(80) NOT NULL,
    "MarkerTone" character varying(40) NOT NULL,
    "MarkerLabel" character varying(120) NOT NULL,
    "Priority" integer NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "ParentNodeKey" character varying(160),
    "PositionX" double precision NOT NULL,
    "PositionY" double precision NOT NULL,
    "StartUtc" timestamp with time zone,
    "EndUtc" timestamp with time zone,
    "IsSystemManaged" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectObjects" PRIMARY KEY ("Id")
);

CREATE TABLE "Workbench_ProjectStructureLeases" (
    "Id" uuid NOT NULL,
    "ScopeKind" integer NOT NULL,
    "ScopeKey" character varying(300) NOT NULL,
    "LeaseToken" character varying(120) NOT NULL,
    "AgentId" character varying(120) NOT NULL,
    "AgentName" character varying(200) NOT NULL,
    "MachineName" character varying(200) NOT NULL,
    "RepositoryRoot" character varying(600) NOT NULL,
    "BranchName" character varying(200) NOT NULL,
    "Reason" TEXT NOT NULL,
    "AcquiredAtUtc" timestamp with time zone NOT NULL,
    "RenewedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "ReleasedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Workbench_ProjectStructureLeases" PRIMARY KEY ("Id")
);

CREATE TABLE "Workbench_ProjectStructureOperationAnalytics" (
    "Id" uuid NOT NULL,
    "OperationName" character varying(160) NOT NULL,
    "ProjectId" uuid,
    "NodeKey" character varying(160),
    "ScopeKind" integer,
    "ScopeKey" character varying(300),
    "AgentId" character varying(120) NOT NULL,
    "AgentName" character varying(200) NOT NULL,
    "MachineName" character varying(200) NOT NULL,
    "RepositoryRoot" character varying(600) NOT NULL,
    "BranchName" character varying(200) NOT NULL,
    "Succeeded" boolean NOT NULL,
    "DurationMs" bigint NOT NULL,
    "WarningCount" integer NOT NULL,
    "ErrorCode" character varying(120),
    "ErrorMessage" TEXT,
    "RequestSummaryJson" TEXT NOT NULL,
    "ResponseSummaryJson" TEXT NOT NULL,
    "WarningsJson" TEXT NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectStructureOperationAnalytics" PRIMARY KEY ("Id")
);

CREATE TABLE "Workbench_ViewStates" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "SurfaceKind" character varying(80) NOT NULL,
    "StateJson" TEXT NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ViewStates" PRIMARY KEY ("Id")
);

CREATE TABLE "Workspace_ProjectStructureAgentProfiles" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "AccessTokenCipherText" TEXT NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "CapabilityMask" integer NOT NULL,
    "AutoApproveMinutes" integer NOT NULL,
    "ApprovalRequiredMinutes" integer NOT NULL,
    "RequireApprovalForAllMutations" boolean NOT NULL,
    "Notes" TEXT NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workspace_ProjectStructureAgentProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE "Workspace_ProjectStructureAgentProjectOverrides" (
    "Id" uuid NOT NULL,
    "ProfileId" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "ProjectName" character varying(200) NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "CapabilityMask" integer NOT NULL,
    "AutoApproveMinutes" integer NOT NULL,
    "ApprovalRequiredMinutes" integer NOT NULL,
    "RequireApprovalForAllMutations" boolean NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_Workspace_ProjectStructureAgentProjectOverrides" PRIMARY KEY ("Id")
);

CREATE TABLE "Workspace_ProjectStructureAgentSettings" (
    "Id" uuid NOT NULL,
    "CentralBaseUrl" character varying(500) NOT NULL,
    "InstallScriptPath" character varying(260) NOT NULL,
    "SetupReadmePath" character varying(260) NOT NULL,
    "DefaultAutoApproveMinutes" integer NOT NULL,
    "DefaultApprovalRequiredMinutes" integer NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workspace_ProjectStructureAgentSettings" PRIMARY KEY ("Id")
);

CREATE TABLE "Workspace_ProviderProfiles" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ProviderKind" integer NOT NULL,
    "BaseUrl" character varying(500) NOT NULL,
    "ApiKeySecretId" uuid,
    "DefaultModel" character varying(120) NOT NULL,
    "TimeoutSeconds" integer NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "SupportsStreaming" boolean NOT NULL,
    "SupportsToolCalling" boolean NOT NULL,
    "SupportsStructuredOutput" boolean NOT NULL,
    "SupportsVision" boolean NOT NULL,
    "LastHealthCheckAtUtc" timestamp with time zone,
    "LastHealthStatus" character varying(120),
    "ExtraSettingsJson" TEXT NOT NULL,
    CONSTRAINT "PK_Workspace_ProviderProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE "Workspace_Settings" (
    "Id" uuid NOT NULL,
    "WorkspaceName" character varying(200) NOT NULL,
    "DefaultProviderProfileId" uuid,
    "DefaultPromptOutputFormat" character varying(40) NOT NULL,
    "Notes" TEXT NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workspace_Settings" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Activity_Entries_CreatedAtUtc" ON "Activity_Entries" ("CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Infrastructure_SearchDocuments_SourceType_SourceKey" ON "Infrastructure_SearchDocuments" ("SourceType", "SourceKey");

CREATE INDEX "IX_Projects_ProjectHierarchyLinks_ChildProjectId" ON "Projects_ProjectHierarchyLinks" ("ChildProjectId");

CREATE INDEX "IX_Projects_ProjectHierarchyLinks_ParentProjectId" ON "Projects_ProjectHierarchyLinks" ("ParentProjectId");

CREATE UNIQUE INDEX "IX_Projects_ProjectHierarchyLinks_ParentProjectId_ChildProject~" ON "Projects_ProjectHierarchyLinks" ("ParentProjectId", "ChildProjectId");

CREATE INDEX "IX_Projects_ProjectOptionSelections_ProjectId_Category" ON "Projects_ProjectOptionSelections" ("ProjectId", "Category");

CREATE INDEX "IX_Projects_ProjectPhases_ProjectId_OrderIndex" ON "Projects_ProjectPhases" ("ProjectId", "OrderIndex");

CREATE UNIQUE INDEX "IX_Prompts_PromptTags_Name" ON "Prompts_PromptTags" ("Name");

CREATE UNIQUE INDEX "IX_Prompts_PromptVersions_PromptArtifactId_VersionNumber" ON "Prompts_PromptVersions" ("PromptArtifactId", "VersionNumber");

CREATE INDEX "IX_Security_SecretReferences_ContextType_ContextId" ON "Security_SecretReferences" ("ContextType", "ContextId");

CREATE INDEX "IX_Validation_Runs_CreatedAtUtc" ON "Validation_Runs" ("CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Workbench_ProjectObjectLinks_ProjectId_SourceNodeKey_Target~" ON "Workbench_ProjectObjectLinks" ("ProjectId", "SourceNodeKey", "TargetNodeKey", "LinkKind", "IsSystemManaged");

CREATE UNIQUE INDEX "IX_Workbench_ProjectObjects_ProjectId_NodeKey" ON "Workbench_ProjectObjects" ("ProjectId", "NodeKey");

CREATE UNIQUE INDEX "IX_Workbench_ProjectStructureLeases_LeaseToken" ON "Workbench_ProjectStructureLeases" ("LeaseToken");

CREATE INDEX "IX_Workbench_ProjectStructureLeases_ScopeKind_ScopeKey" ON "Workbench_ProjectStructureLeases" ("ScopeKind", "ScopeKey");

CREATE INDEX "IX_Workbench_ProjectStructureOperationAnalytics_OccurredAtUtc" ON "Workbench_ProjectStructureOperationAnalytics" ("OccurredAtUtc");

CREATE INDEX "IX_Workbench_ProjectStructureOperationAnalytics_ProjectId_Oper~" ON "Workbench_ProjectStructureOperationAnalytics" ("ProjectId", "OperationName");

CREATE UNIQUE INDEX "IX_Workbench_ViewStates_ProjectId_SurfaceKind" ON "Workbench_ViewStates" ("ProjectId", "SurfaceKind");

CREATE UNIQUE INDEX "IX_Workspace_ProjectStructureAgentProjectOverrides_ProfileId_P~" ON "Workspace_ProjectStructureAgentProjectOverrides" ("ProfileId", "ProjectId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260401094848_InitialCreate', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" ADD "StorageObjectReferenceJson" TEXT NOT NULL DEFAULT '';

CREATE TABLE "Storage_Catalog" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ProviderKind" integer NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "IsSystemDefault" boolean NOT NULL,
    "IsReadOnly" boolean NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "ConnectionMode" integer NOT NULL,
    "EndpointOrRoot" character varying(1200) NOT NULL,
    "ConfigJson" TEXT NOT NULL,
    "CapabilityMask" integer NOT NULL,
    "HealthStatus" integer NOT NULL,
    "LastTestedAtUtc" timestamp with time zone,
    "LastHealthMessage" character varying(500) NOT NULL,
    "CredentialSecretId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Storage_Catalog" PRIMARY KEY ("Id")
);

CREATE TABLE "Storage_RoutingRules" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "Priority" integer NOT NULL,
    "ScopeKind" integer NOT NULL,
    "ProjectId" uuid,
    "NodeKey" character varying(160) NOT NULL,
    "UsagePurpose" integer NOT NULL,
    "ContentKind" integer NOT NULL,
    "MimePattern" character varying(200) NOT NULL,
    "MinimumContentLength" bigint,
    "MaximumContentLength" bigint,
    "EditIntent" boolean NOT NULL,
    "PreviewRequired" boolean NOT NULL,
    "PublishIntent" boolean NOT NULL,
    "RequiredCapabilities" integer NOT NULL,
    "PreferredStorageId" uuid NOT NULL,
    "AlternativeStorageIdsJson" TEXT NOT NULL,
    "Reason" character varying(500) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Storage_RoutingRules" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Storage_Catalog_Name" ON "Storage_Catalog" ("Name");

CREATE INDEX "IX_Storage_Catalog_ProviderKind_IsEnabled" ON "Storage_Catalog" ("ProviderKind", "IsEnabled");

CREATE INDEX "IX_Storage_RoutingRules_ScopeKind_ProjectId_NodeKey_Priority_P~" ON "Storage_RoutingRules" ("ScopeKind", "ProjectId", "NodeKey", "Priority", "PreferredStorageId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260402033724_AddStorageFoundation', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" ADD "DurationSeconds" integer;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260402235724_AddProjectObjectDurationSeconds', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "CrmHr_AiAgentProfiles" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "ProviderProfileId" uuid,
    "DefaultModel" character varying(160) NOT NULL,
    "ExecutionMode" character varying(32) NOT NULL,
    "OwnerPartyId" uuid,
    "CapabilityJson" TEXT NOT NULL,
    "ValidationStatus" character varying(32) NOT NULL,
    "LastReviewedAtUtc" timestamp with time zone,
    "Notes" TEXT NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_AiAgentProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_AuditEntries" (
    "Id" uuid NOT NULL,
    "EntityType" character varying(120) NOT NULL,
    "EntityId" uuid NOT NULL,
    "Action" character varying(80) NOT NULL,
    "Summary" character varying(400) NOT NULL,
    "DetailJson" TEXT NOT NULL,
    "Actor" character varying(160) NOT NULL,
    "IsSensitive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_AuditEntries" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_CapacityBlocks" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "BlockKind" character varying(32) NOT NULL,
    "StartDateUtc" timestamp with time zone NOT NULL,
    "EndDateUtc" timestamp with time zone NOT NULL,
    "Percentage" numeric NOT NULL,
    "RelatedProjectId" uuid,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_CapacityBlocks" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_ConfidentialNotes" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "Category" character varying(80) NOT NULL,
    "NoteText" TEXT NOT NULL,
    "CreatedBy" character varying(160) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_ConfidentialNotes" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_InteractionParties" (
    "Id" uuid NOT NULL,
    "InteractionId" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "Role" character varying(64) NOT NULL,
    CONSTRAINT "PK_CrmHr_InteractionParties" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_Interactions" (
    "Id" uuid NOT NULL,
    "InteractionType" character varying(64) NOT NULL,
    "Subject" character varying(200) NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "Summary" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "NextActionText" character varying(240) NOT NULL,
    "NextActionOwnerPartyId" uuid,
    "NextActionDueUtc" timestamp with time zone,
    "RelatedOpportunityId" uuid,
    "RelatedProjectId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_Interactions" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_LookupOptions" (
    "Id" uuid NOT NULL,
    "CatalogKind" character varying(64) NOT NULL,
    "Key" character varying(120) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "IsSystemDefault" boolean NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_LookupOptions" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_OnboardingTasks" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "TaskKind" character varying(32) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "OwnerPartyId" uuid,
    "DueDateUtc" timestamp with time zone,
    "Status" character varying(32) NOT NULL,
    "Notes" TEXT NOT NULL,
    "RelatedProjectId" uuid,
    CONSTRAINT "PK_CrmHr_OnboardingTasks" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_Opportunities" (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Stage" character varying(64) NOT NULL,
    "RelationshipStage" character varying(80) NOT NULL,
    "AccountPartyId" uuid NOT NULL,
    "OwnerPartyId" uuid NOT NULL,
    "DeliveryUnitPartyId" uuid,
    "LinkedProjectId" uuid,
    "CurrencyCode" character varying(16) NOT NULL,
    "Amount" numeric,
    "ProbabilityPercent" integer NOT NULL,
    "ExpectedCloseDateUtc" timestamp with time zone,
    "OpportunitySource" character varying(64) NOT NULL,
    "LostReason" character varying(240) NOT NULL,
    "Summary" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_Opportunities" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_OpportunityParties" (
    "Id" uuid NOT NULL,
    "OpportunityId" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "Role" character varying(64) NOT NULL,
    CONSTRAINT "PK_CrmHr_OpportunityParties" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_OpportunityStageHistory" (
    "Id" uuid NOT NULL,
    "OpportunityId" uuid NOT NULL,
    "Stage" character varying(64) NOT NULL,
    "ChangedAtUtc" timestamp with time zone NOT NULL,
    "ChangedBy" character varying(160) NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_OpportunityStageHistory" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_Parties" (
    "Id" uuid NOT NULL,
    "PartyType" character varying(64) NOT NULL,
    "LifecycleStatus" character varying(64) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "LegalName" character varying(200) NOT NULL,
    "PreferredName" character varying(200) NOT NULL,
    "ExternalCode" character varying(120) NOT NULL,
    "Summary" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "TagsJson" TEXT NOT NULL,
    "Region" character varying(120) NOT NULL,
    "CountryCode" character varying(16) NOT NULL,
    "TimeZone" character varying(80) NOT NULL,
    "IsSensitive" boolean NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    "LastChangedBy" character varying(160) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_Parties" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_PartyAddresses" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "AddressType" character varying(80) NOT NULL,
    "Line1" character varying(200) NOT NULL,
    "Line2" character varying(200) NOT NULL,
    "City" character varying(120) NOT NULL,
    "Region" character varying(120) NOT NULL,
    "PostalCode" character varying(40) NOT NULL,
    "CountryCode" character varying(16) NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_PartyAddresses" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_PartyContactPoints" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "ContactType" character varying(64) NOT NULL,
    "Label" character varying(120) NOT NULL,
    "Value" character varying(400) NOT NULL,
    "NormalizedValue" character varying(400) NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "IsPublic" boolean NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_PartyContactPoints" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_PartyRelationships" (
    "Id" uuid NOT NULL,
    "SourcePartyId" uuid NOT NULL,
    "TargetPartyId" uuid NOT NULL,
    "RelationshipKind" character varying(64) NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "StartDateUtc" timestamp with time zone,
    "EndDateUtc" timestamp with time zone,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_PartyRelationships" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_PartyRoles" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "RoleKind" character varying(80) NOT NULL,
    "Title" character varying(160) NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "ValidFromUtc" timestamp with time zone,
    "ValidToUtc" timestamp with time zone,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_PartyRoles" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_PartySkills" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "SkillId" uuid NOT NULL,
    "Proficiency" character varying(32) NOT NULL,
    "YearsExperience" integer NOT NULL,
    "CertificationStatus" character varying(120) NOT NULL,
    "LastValidatedAtUtc" timestamp with time zone,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_PartySkills" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_ProjectPartyAssignments" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "AssignmentKind" character varying(48) NOT NULL,
    "NodeKey" character varying(160) NOT NULL,
    "PhaseName" character varying(160) NOT NULL,
    "OpportunityId" uuid,
    "AllocationPercent" numeric,
    "StartsAtUtc" timestamp with time zone,
    "EndsAtUtc" timestamp with time zone,
    "IsPrimary" boolean NOT NULL,
    "Source" character varying(80) NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_ProjectPartyAssignments" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_RecruitmentApplications" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "TargetUnitPartyId" uuid,
    "RecruiterPartyId" uuid,
    "HiringManagerPartyId" uuid,
    "DesiredRole" character varying(160) NOT NULL,
    "Source" character varying(120) NOT NULL,
    "Stage" character varying(32) NOT NULL,
    "AvailableFromUtc" timestamp with time zone,
    "Decision" character varying(32) NOT NULL,
    "Notes" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_RecruitmentApplications" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_RecruitmentInterviews" (
    "Id" uuid NOT NULL,
    "ApplicationId" uuid NOT NULL,
    "ScheduledAtUtc" timestamp with time zone NOT NULL,
    "InterviewType" character varying(32) NOT NULL,
    "InterviewerPartyId" uuid,
    "Outcome" character varying(32) NOT NULL,
    "Feedback" TEXT NOT NULL,
    "Recommendation" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_RecruitmentInterviews" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_Skills" (
    "Id" uuid NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Category" character varying(120) NOT NULL,
    "Description" TEXT NOT NULL,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_CrmHr_Skills" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_StaffingRequests" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid,
    "RequestedByPartyId" uuid,
    "DeliveryUnitPartyId" uuid,
    "Title" character varying(200) NOT NULL,
    "NeededRole" character varying(160) NOT NULL,
    "NeededSkillsJson" TEXT NOT NULL,
    "StartDateUtc" timestamp with time zone,
    "EndDateUtc" timestamp with time zone,
    "AllocationPercent" numeric NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_StaffingRequests" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_WorkforceProfiles" (
    "Id" uuid NOT NULL,
    "PartyId" uuid NOT NULL,
    "WorkforceKind" character varying(64) NOT NULL,
    "EmployeeCode" character varying(80) NOT NULL,
    "JobTitle" character varying(160) NOT NULL,
    "Discipline" character varying(120) NOT NULL,
    "Seniority" character varying(80) NOT NULL,
    "HomeUnitPartyId" uuid,
    "ManagerPartyId" uuid,
    "StartDateUtc" timestamp with time zone,
    "EndDateUtc" timestamp with time zone,
    "Location" character varying(160) NOT NULL,
    "TimeZone" character varying(80) NOT NULL,
    "InternalCostRate" numeric,
    "ExternalBillingRate" numeric,
    "CapacityHoursPerWeek" numeric NOT NULL,
    "Status" character varying(80) NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    CONSTRAINT "PK_CrmHr_WorkforceProfiles" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_CrmHr_AiAgentProfiles_PartyId" ON "CrmHr_AiAgentProfiles" ("PartyId");

CREATE INDEX "IX_CrmHr_AiAgentProfiles_ProviderProfileId" ON "CrmHr_AiAgentProfiles" ("ProviderProfileId");

CREATE INDEX "IX_CrmHr_AuditEntries_EntityType_EntityId" ON "CrmHr_AuditEntries" ("EntityType", "EntityId");

CREATE INDEX "IX_CrmHr_CapacityBlocks_PartyId_StartDateUtc_EndDateUtc" ON "CrmHr_CapacityBlocks" ("PartyId", "StartDateUtc", "EndDateUtc");

CREATE INDEX "IX_CrmHr_ConfidentialNotes_PartyId" ON "CrmHr_ConfidentialNotes" ("PartyId");

CREATE INDEX "IX_CrmHr_InteractionParties_InteractionId_PartyId_Role" ON "CrmHr_InteractionParties" ("InteractionId", "PartyId", "Role");

CREATE INDEX "IX_CrmHr_Interactions_RelatedOpportunityId" ON "CrmHr_Interactions" ("RelatedOpportunityId");

CREATE INDEX "IX_CrmHr_Interactions_RelatedProjectId" ON "CrmHr_Interactions" ("RelatedProjectId");

CREATE UNIQUE INDEX "IX_CrmHr_LookupOptions_CatalogKind_Key" ON "CrmHr_LookupOptions" ("CatalogKind", "Key");

CREATE INDEX "IX_CrmHr_OnboardingTasks_PartyId_TaskKind_Status" ON "CrmHr_OnboardingTasks" ("PartyId", "TaskKind", "Status");

CREATE INDEX "IX_CrmHr_Opportunities_AccountPartyId" ON "CrmHr_Opportunities" ("AccountPartyId");

CREATE INDEX "IX_CrmHr_Opportunities_LinkedProjectId" ON "CrmHr_Opportunities" ("LinkedProjectId");

CREATE INDEX "IX_CrmHr_Opportunities_OwnerPartyId" ON "CrmHr_Opportunities" ("OwnerPartyId");

CREATE INDEX "IX_CrmHr_Opportunities_Stage" ON "CrmHr_Opportunities" ("Stage");

CREATE INDEX "IX_CrmHr_OpportunityParties_OpportunityId_PartyId_Role" ON "CrmHr_OpportunityParties" ("OpportunityId", "PartyId", "Role");

CREATE INDEX "IX_CrmHr_OpportunityStageHistory_OpportunityId_ChangedAtUtc" ON "CrmHr_OpportunityStageHistory" ("OpportunityId", "ChangedAtUtc");

CREATE INDEX "IX_CrmHr_Parties_DisplayName" ON "CrmHr_Parties" ("DisplayName");

CREATE INDEX "IX_CrmHr_Parties_ExternalCode" ON "CrmHr_Parties" ("ExternalCode");

CREATE INDEX "IX_CrmHr_Parties_PartyType_LifecycleStatus" ON "CrmHr_Parties" ("PartyType", "LifecycleStatus");

CREATE INDEX "IX_CrmHr_PartyAddresses_PartyId_IsPrimary" ON "CrmHr_PartyAddresses" ("PartyId", "IsPrimary");

CREATE INDEX "IX_CrmHr_PartyContactPoints_NormalizedValue" ON "CrmHr_PartyContactPoints" ("NormalizedValue");

CREATE INDEX "IX_CrmHr_PartyContactPoints_PartyId_IsPrimary" ON "CrmHr_PartyContactPoints" ("PartyId", "IsPrimary");

CREATE INDEX "IX_CrmHr_PartyRelationships_SourcePartyId_TargetPartyId_Relati~" ON "CrmHr_PartyRelationships" ("SourcePartyId", "TargetPartyId", "RelationshipKind");

CREATE INDEX "IX_CrmHr_PartyRelationships_TargetPartyId" ON "CrmHr_PartyRelationships" ("TargetPartyId");

CREATE INDEX "IX_CrmHr_PartyRoles_PartyId_RoleKind" ON "CrmHr_PartyRoles" ("PartyId", "RoleKind");

CREATE UNIQUE INDEX "IX_CrmHr_PartySkills_PartyId_SkillId" ON "CrmHr_PartySkills" ("PartyId", "SkillId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_OpportunityId" ON "CrmHr_ProjectPartyAssignments" ("OpportunityId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_PartyId" ON "CrmHr_ProjectPartyAssignments" ("PartyId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_ProjectId" ON "CrmHr_ProjectPartyAssignments" ("ProjectId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_ProjectId_PartyId_AssignmentK~" ON "CrmHr_ProjectPartyAssignments" ("ProjectId", "PartyId", "AssignmentKind", "NodeKey");

CREATE INDEX "IX_CrmHr_RecruitmentApplications_PartyId_Stage" ON "CrmHr_RecruitmentApplications" ("PartyId", "Stage");

CREATE INDEX "IX_CrmHr_RecruitmentInterviews_ApplicationId_ScheduledAtUtc" ON "CrmHr_RecruitmentInterviews" ("ApplicationId", "ScheduledAtUtc");

CREATE UNIQUE INDEX "IX_CrmHr_Skills_Name" ON "CrmHr_Skills" ("Name");

CREATE INDEX "IX_CrmHr_StaffingRequests_DeliveryUnitPartyId" ON "CrmHr_StaffingRequests" ("DeliveryUnitPartyId");

CREATE INDEX "IX_CrmHr_StaffingRequests_ProjectId" ON "CrmHr_StaffingRequests" ("ProjectId");

CREATE INDEX "IX_CrmHr_WorkforceProfiles_HomeUnitPartyId" ON "CrmHr_WorkforceProfiles" ("HomeUnitPartyId");

CREATE INDEX "IX_CrmHr_WorkforceProfiles_ManagerPartyId" ON "CrmHr_WorkforceProfiles" ("ManagerPartyId");

CREATE INDEX "IX_CrmHr_WorkforceProfiles_PartyId" ON "CrmHr_WorkforceProfiles" ("PartyId");

CREATE INDEX "IX_CrmHr_WorkforceProfiles_Status" ON "CrmHr_WorkforceProfiles" ("Status");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260403170205_AddCrmHrFoundation', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "CrmHr_AccountProfiles" (
    "Id" uuid NOT NULL,
    "AccountPartyId" uuid NOT NULL,
    "RelationshipStage" character varying(64) NOT NULL,
    "CommercialNotes" TEXT NOT NULL,
    "ConstraintNotes" TEXT NOT NULL,
    "TimingRiskNotes" TEXT NOT NULL,
    "LastChangedBy" character varying(160) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_AccountProfiles" PRIMARY KEY ("Id")
);

CREATE TABLE "CrmHr_AccountStakeholders" (
    "Id" uuid NOT NULL,
    "AccountPartyId" uuid NOT NULL,
    "RelatedPartyId" uuid NOT NULL,
    "Role" character varying(64) NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "Notes" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CrmHr_AccountStakeholders" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_CrmHr_AccountProfiles_AccountPartyId" ON "CrmHr_AccountProfiles" ("AccountPartyId");

CREATE UNIQUE INDEX "IX_CrmHr_AccountStakeholders_AccountPartyId_RelatedPartyId_Role" ON "CrmHr_AccountStakeholders" ("AccountPartyId", "RelatedPartyId", "Role");

CREATE INDEX "IX_CrmHr_AccountStakeholders_RelatedPartyId" ON "CrmHr_AccountStakeholders" ("RelatedPartyId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260403194503_AddCrmHrAccountsAndInteractions', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Validation_Runs" ADD "ResponsiblePartyId" uuid;

ALTER TABLE "TestLab_TestPlans" ADD "ResponsiblePartyId" uuid;

ALTER TABLE "Resources_ProjectResources" ADD "MaintainerPartyId" uuid;

ALTER TABLE "Resources_ProjectResources" ADD "OwnerPartyId" uuid;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260404044539_AddCrmHrCrossModuleResponsibleParties', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "Workbench_ProjectProjectionLayouts" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "NodeKey" character varying(160) NOT NULL,
    "PositionX" double precision NOT NULL,
    "PositionY" double precision NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectProjectionLayouts" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Workbench_ProjectProjectionLayouts_ProjectId_NodeKey" ON "Workbench_ProjectProjectionLayouts" ("ProjectId", "NodeKey");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405021055_AddWorkbenchProjectionLayouts', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "Workbench_ProjectNodeBindings" (
    "Id" uuid NOT NULL,
    "ProjectObjectId" uuid NOT NULL,
    "Route" character varying(800) NOT NULL,
    "ExternalArtifactKind" character varying(120) NOT NULL,
    "ExternalArtifactId" uuid,
    "MediaRelativePath" character varying(800) NOT NULL,
    "MediaContentType" character varying(160) NOT NULL,
    "MediaOriginalFileName" character varying(260) NOT NULL,
    "StorageObjectReferenceJson" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectNodeBindings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Workbench_ProjectNodeBindings_Workbench_ProjectObjects_Proj~" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Workbench_ProjectNodeReferences" (
    "Id" uuid NOT NULL,
    "ProjectObjectId" uuid NOT NULL,
    "ReferenceKind" integer NOT NULL,
    "ReferenceId" uuid NOT NULL,
    "OrderIndex" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectNodeReferences" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Workbench_ProjectNodeReferences_Workbench_ProjectObjects_Pr~" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Workbench_ProjectNodeBindings_ProjectObjectId" ON "Workbench_ProjectNodeBindings" ("ProjectObjectId");

CREATE UNIQUE INDEX "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceK~1" ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "ReferenceId");

CREATE INDEX "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKi~" ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "OrderIndex");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405024129_AddProjectNodeBindings', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "Workbench_ProjectNodeLifecycleEvents" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "ProjectObjectId" uuid NOT NULL,
    "NodeKey" character varying(160) NOT NULL,
    "TransitionMode" integer NOT NULL,
    "SourceFamily" integer NOT NULL,
    "TargetFamily" integer NOT NULL,
    "SourceObjectType" integer NOT NULL,
    "SourceObjectSubtype" character varying(120) NOT NULL,
    "TargetObjectType" integer NOT NULL,
    "TargetObjectSubtype" character varying(120) NOT NULL,
    "SourceSnapshotJson" TEXT NOT NULL,
    "TargetSnapshotJson" TEXT NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectNodeLifecycleEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Workbench_ProjectNodeLifecycleEvents_Workbench_ProjectObjec~" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Workbench_ProjectNodeLifecycleEvents_ProjectId_NodeKey_Occu~" ON "Workbench_ProjectNodeLifecycleEvents" ("ProjectId", "NodeKey", "OccurredAtUtc");

CREATE INDEX "IX_Workbench_ProjectNodeLifecycleEvents_ProjectObjectId" ON "Workbench_ProjectNodeLifecycleEvents" ("ProjectObjectId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405033238_AddProjectNodeLifecycleEvents', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Workspace_ProviderProfiles" ADD "ConfigSchemaVersion" character varying(40) NOT NULL DEFAULT '';

ALTER TABLE "Workspace_ProviderProfiles" ADD "ConnectorPluginKey" character varying(160) NOT NULL DEFAULT '';

ALTER TABLE "Resources_ProjectResources" ADD "ConfigSchemaVersion" character varying(40) NOT NULL DEFAULT '';

ALTER TABLE "Resources_ProjectResources" ADD "ConnectorPluginKey" character varying(160) NOT NULL DEFAULT '';

CREATE TABLE "Workbench_ProjectCrossModuleMutations" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "ScopeNodeKey" character varying(160) NOT NULL,
    "MutationKind" integer NOT NULL,
    "Status" integer NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "ErrorMessage" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workbench_ProjectCrossModuleMutations" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ScopeNodeKe~" ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ScopeNodeKey", "CreatedAtUtc");

CREATE INDEX "IX_Workbench_ProjectCrossModuleMutations_ProjectId_Status_Upda~" ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "Status", "UpdatedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405041816_AddConnectorPluginPlatformAndCrossModuleMutations', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" ALTER COLUMN "StorageObjectReferenceJson" TYPE text;

ALTER TABLE "Workbench_ProjectObjects" ALTER COLUMN "Route" TYPE text;

ALTER TABLE "Workbench_ProjectObjects" ALTER COLUMN "MediaRelativePath" TYPE text;

ALTER TABLE "Workbench_ProjectObjects" ALTER COLUMN "MediaOriginalFileName" TYPE text;

ALTER TABLE "Workbench_ProjectObjects" ALTER COLUMN "MediaContentType" TYPE text;

ALTER TABLE "Workbench_ProjectObjects" ALTER COLUMN "ExternalArtifactKind" TYPE text;

ALTER TABLE "Workbench_ProjectObjects" ADD "MarkersJson" TEXT NOT NULL DEFAULT '[]';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405150302_AddProjectObjectMarkersJson', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "ApprovalState" integer NOT NULL DEFAULT 0;

ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "AttemptCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "CompletedAtUtc" timestamp with time zone;

ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "LastAttemptAtUtc" timestamp with time zone;

CREATE INDEX "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalSta~" ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ApprovalState", "Status", "UpdatedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405194312_AddCrossModuleMutationDurabilityFields', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "ExternalArtifactId";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "ExternalArtifactKind";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "MarkerIcon";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "MarkerLabel";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "MarkerTone";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "MediaContentType";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "MediaOriginalFileName";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "MediaRelativePath";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "Route";

ALTER TABLE "Workbench_ProjectObjects" DROP COLUMN "StorageObjectReferenceJson";

ALTER TABLE "Workspace_ProviderProfiles" ALTER COLUMN "ProviderKind" DROP NOT NULL;

ALTER TABLE "Workbench_ProjectNodeReferences" ALTER COLUMN "ReferenceKind" TYPE character varying(160);

ALTER TABLE "Workbench_ProjectNodeReferences" ALTER COLUMN "ReferenceId" TYPE character varying(200);

ALTER TABLE "Resources_ProjectResources" ALTER COLUMN "ResourceKind" DROP NOT NULL;

CREATE TABLE "Workspace_ConnectorCommands" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "ConnectorPluginKey" character varying(160) NOT NULL,
    "CommandKey" character varying(160) NOT NULL,
    "IdempotencyKey" character varying(200) NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "Status" integer NOT NULL,
    "ApprovalState" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "LastAttemptAtUtc" timestamp with time zone,
    "NextAttemptAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "LastError" TEXT NOT NULL,
    "ResultJson" TEXT NOT NULL,
    "RequestedBy" character varying(160) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workspace_ConnectorCommands" PRIMARY KEY ("Id")
);

CREATE TABLE "Workspace_ConnectorCommandAudits" (
    "Id" uuid NOT NULL,
    "ConnectorCommandId" uuid NOT NULL,
    "ProjectId" uuid NOT NULL,
    "EventKind" integer NOT NULL,
    "Actor" character varying(160) NOT NULL,
    "Message" character varying(400) NOT NULL,
    "DetailsJson" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Workspace_ConnectorCommandAudits" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Workspace_ConnectorCommandAudits_Workspace_ConnectorCommand~" FOREIGN KEY ("ConnectorCommandId") REFERENCES "Workspace_ConnectorCommands" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_Created~" ON "Workspace_ConnectorCommandAudits" ("ConnectorCommandId", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Workspace_ConnectorCommands_ProjectId_ConnectorPluginKey_Co~" ON "Workspace_ConnectorCommands" ("ProjectId", "ConnectorPluginKey", "CommandKey", "IdempotencyKey");

CREATE INDEX "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc" ON "Workspace_ConnectorCommands" ("ProjectId", "CreatedAtUtc");

CREATE INDEX "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemp~" ON "Workspace_ConnectorCommands" ("Status", "ApprovalState", "NextAttemptAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405222902_AddConnectorCommandOutboxBoundary', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "Automation_DeadLetters" (
    "Id" uuid NOT NULL,
    "EnvelopeId" uuid NOT NULL,
    "DeliveryId" uuid NOT NULL,
    "EnvelopeType" character varying(240) NOT NULL,
    "HandlerKey" character varying(240) NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "ErrorMessage" TEXT NOT NULL,
    "AttemptCount" integer NOT NULL,
    "DedupeKey" character varying(240),
    "CorrelationId" uuid,
    "CausationId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "DeadLetteredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Automation_DeadLetters" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_DeliveryAttempts" (
    "Id" uuid NOT NULL,
    "EnvelopeId" uuid NOT NULL,
    "DeliveryId" uuid NOT NULL,
    "HandlerKey" character varying(240) NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "Outcome" integer NOT NULL,
    "CorrelationId" uuid,
    "CausationId" uuid,
    "ErrorMessage" TEXT NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Automation_DeliveryAttempts" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_Envelopes" (
    "Id" uuid NOT NULL,
    "EnvelopeType" character varying(240) NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "State" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "DedupeKey" character varying(240),
    "CorrelationId" uuid,
    "CausationId" uuid,
    "AvailableAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Automation_Envelopes" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_ExecutionLogs" (
    "Id" uuid NOT NULL,
    "EventKind" integer NOT NULL,
    "SourceType" character varying(160) NOT NULL,
    "SourceId" character varying(160) NOT NULL,
    "CorrelationId" uuid,
    "CausationId" uuid,
    "Message" character varying(400) NOT NULL,
    "DetailsJson" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Automation_ExecutionLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_PluginIngressCursors" (
    "Id" uuid NOT NULL,
    "SourceKind" character varying(160) NOT NULL,
    "SourceKey" character varying(160) NOT NULL,
    "CursorValue" character varying(240) NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Automation_PluginIngressCursors" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_PluginIngressEnvelopes" (
    "Id" uuid NOT NULL,
    "SourceKind" character varying(160) NOT NULL,
    "SourceKey" character varying(160) NOT NULL,
    "ExternalMessageId" character varying(240) NOT NULL,
    "CursorValue" character varying(240) NOT NULL,
    "DedupeKey" character varying(280) NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "State" integer NOT NULL,
    "CorrelationId" uuid,
    "MaterializerKey" character varying(200) NOT NULL,
    "MaterializationSummary" TEXT NOT NULL,
    "LastError" TEXT NOT NULL,
    "ReceivedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    "MaterializedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Automation_PluginIngressEnvelopes" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_Triggers" (
    "Id" uuid NOT NULL,
    "OwnerKind" integer NOT NULL,
    "OwnerKey" character varying(160) NOT NULL,
    "TriggerKey" character varying(160) NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "TriggerKind" integer NOT NULL,
    "CronExpression" character varying(160) NOT NULL,
    "TimeZoneId" character varying(120) NOT NULL,
    "StartAtUtc" timestamp with time zone,
    "EndAtUtc" timestamp with time zone,
    "MisfirePolicy" integer NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "DedupeKey" character varying(240) NOT NULL,
    "NextPlannedFireAtUtc" timestamp with time zone,
    "LastFiredAtUtc" timestamp with time zone,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Automation_Triggers" PRIMARY KEY ("Id")
);

CREATE TABLE "Automation_EnvelopeDeliveries" (
    "Id" uuid NOT NULL,
    "EnvelopeId" uuid NOT NULL,
    "EnvelopeType" character varying(240) NOT NULL,
    "HandlerKey" character varying(240) NOT NULL,
    "State" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "MaxAttempts" integer NOT NULL,
    "AvailableAtUtc" timestamp with time zone NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    "LastAttemptAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "LastError" TEXT NOT NULL,
    "LockToken" character varying(100) NOT NULL,
    "LockedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Automation_EnvelopeDeliveries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Automation_EnvelopeDeliveries_Automation_Envelopes_Envelope~" FOREIGN KEY ("EnvelopeId") REFERENCES "Automation_Envelopes" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Automation_DeadLetters_DeadLetteredAtUtc_HandlerKey" ON "Automation_DeadLetters" ("DeadLetteredAtUtc", "HandlerKey");

CREATE UNIQUE INDEX "IX_Automation_DeadLetters_DeliveryId" ON "Automation_DeadLetters" ("DeliveryId");

CREATE UNIQUE INDEX "IX_Automation_DeliveryAttempts_DeliveryId_AttemptNumber" ON "Automation_DeliveryAttempts" ("DeliveryId", "AttemptNumber");

CREATE UNIQUE INDEX "IX_Automation_EnvelopeDeliveries_EnvelopeId_HandlerKey" ON "Automation_EnvelopeDeliveries" ("EnvelopeId", "HandlerKey");

CREATE INDEX "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc" ON "Automation_EnvelopeDeliveries" ("State", "AvailableAtUtc");

CREATE UNIQUE INDEX "IX_Automation_Envelopes_EnvelopeType_DedupeKey" ON "Automation_Envelopes" ("EnvelopeType", "DedupeKey");

CREATE INDEX "IX_Automation_Envelopes_State_AvailableAtUtc" ON "Automation_Envelopes" ("State", "AvailableAtUtc");

CREATE INDEX "IX_Automation_ExecutionLogs_SourceType_SourceId_CreatedAtUtc" ON "Automation_ExecutionLogs" ("SourceType", "SourceId", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Automation_PluginIngressCursors_SourceKind_SourceKey" ON "Automation_PluginIngressCursors" ("SourceKind", "SourceKey");

CREATE UNIQUE INDEX "IX_Automation_PluginIngressEnvelopes_SourceKind_SourceKey_Dedu~" ON "Automation_PluginIngressEnvelopes" ("SourceKind", "SourceKey", "DedupeKey");

CREATE INDEX "IX_Automation_PluginIngressEnvelopes_State_ReceivedAtUtc" ON "Automation_PluginIngressEnvelopes" ("State", "ReceivedAtUtc");

CREATE UNIQUE INDEX "IX_Automation_Triggers_OwnerKind_OwnerKey_TriggerKey" ON "Automation_Triggers" ("OwnerKind", "OwnerKey", "TriggerKey");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260406023449_AddAutomationRuntimePlane', '10.0.4');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemp~";

DROP INDEX "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc";

ALTER TABLE "Workspace_ConnectorCommands" ADD "LeaseExpiresAtUtc" timestamp with time zone;

ALTER TABLE "Workspace_ConnectorCommands" ADD "LeaseToken" character varying(100) NOT NULL DEFAULT '';

CREATE INDEX "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemp~" ON "Workspace_ConnectorCommands" ("Status", "ApprovalState", "NextAttemptAtUtc", "LeaseExpiresAtUtc");

CREATE INDEX "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc_LockedAt~" ON "Automation_EnvelopeDeliveries" ("State", "AvailableAtUtc", "LockedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260406144942_AddAutomationRuntimeHardeningPhase13', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "Processes_ArtifactExpectations" (
    "Id" uuid NOT NULL,
    "StepDefinitionId" uuid NOT NULL,
    "ArtifactKind" character varying(48) NOT NULL,
    "Title" character varying(160) NOT NULL,
    "IsRequired" boolean NOT NULL,
    "TrustRequirement" character varying(48) NOT NULL,
    "SensitivityLevel" character varying(48) NOT NULL,
    "RetentionDays" integer NOT NULL,
    "AllowedFutureUsageSummary" TEXT NOT NULL,
    "ValidationRequirementSummary" TEXT NOT NULL,
    CONSTRAINT "PK_Processes_ArtifactExpectations" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_ArtifactRecords" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "StepRunId" uuid,
    "ArtifactKind" character varying(48) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "TrustStatus" character varying(48) NOT NULL,
    "SensitivityLevel" character varying(48) NOT NULL,
    "ProvenanceSummary" TEXT NOT NULL,
    "AllowedFutureUsageSummary" TEXT NOT NULL,
    "ReviewSummary" TEXT NOT NULL,
    "ManagedStoragePath" character varying(500) NOT NULL,
    "ExternalReferenceKey" character varying(200) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_ArtifactRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_ConformanceObservations" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "StepRunId" uuid,
    "Severity" character varying(48) NOT NULL,
    "Category" character varying(120) NOT NULL,
    "Observation" TEXT NOT NULL,
    "DeviationReason" TEXT NOT NULL,
    "IsSafeNonAction" boolean NOT NULL,
    "ContainsSensitiveAssessment" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_ConformanceObservations" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_DecisionRecords" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "StepRunId" uuid,
    "DecisionKind" character varying(48) NOT NULL,
    "Outcome" character varying(48) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Reason" TEXT NOT NULL,
    "PolicyEvaluation" TEXT NOT NULL,
    "DecidedBy" character varying(160) NOT NULL,
    "OperatingMode" character varying(48) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_DecisionRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_Definitions" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid,
    "Name" character varying(200) NOT NULL,
    "Slug" character varying(200) NOT NULL,
    "Summary" TEXT NOT NULL,
    "ValueStatement" TEXT NOT NULL,
    "CustomerName" character varying(200) NOT NULL,
    "OwnerName" character varying(200) NOT NULL,
    "InterfaceContractSummary" TEXT NOT NULL,
    "GovernanceNotes" TEXT NOT NULL,
    "Criticality" character varying(48) NOT NULL,
    "AutonomyLevel" character varying(48) NOT NULL,
    "Status" character varying(48) NOT NULL,
    "ActivePublishedVersionId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_Definitions" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_DefinitionVersions" (
    "Id" uuid NOT NULL,
    "ProcessDefinitionId" uuid NOT NULL,
    "VersionNumber" integer NOT NULL,
    "Status" character varying(48) NOT NULL,
    "ChangeSummary" TEXT NOT NULL,
    "GovernancePolicySummary" TEXT NOT NULL,
    "ConstitutionRuleSummary" TEXT NOT NULL,
    "OperatingModeSummary" TEXT NOT NULL,
    "SimulationReadinessSummary" TEXT NOT NULL,
    "ImportedFrom" character varying(200) NOT NULL,
    "ImportWarnings" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    "PublishedAtUtc" timestamp with time zone,
    "PublishedBy" character varying(160) NOT NULL,
    CONSTRAINT "PK_Processes_DefinitionVersions" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_ImprovementCandidates" (
    "Id" uuid NOT NULL,
    "ProcessDefinitionId" uuid NOT NULL,
    "ProcessRunId" uuid,
    "Title" character varying(200) NOT NULL,
    "Category" character varying(120) NOT NULL,
    "ProblemSummary" TEXT NOT NULL,
    "EvidenceSummary" TEXT NOT NULL,
    "Status" character varying(48) NOT NULL,
    "IsTrainingOpportunity" boolean NOT NULL,
    "RequiresGovernanceReview" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ClosedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Processes_ImprovementCandidates" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_JournalEntries" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "StepRunId" uuid,
    "EventType" character varying(120) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "CorrelationId" character varying(120) NOT NULL,
    "OperatingMode" character varying(48) NOT NULL,
    "PolicyVersion" character varying(120) NOT NULL,
    "EnvironmentMode" character varying(120) NOT NULL,
    "ReplayContextJson" TEXT NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_JournalEntries" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_RoleRequirements" (
    "Id" uuid NOT NULL,
    "ProcessDefinitionVersionId" uuid NOT NULL,
    "Key" character varying(120) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "Purpose" TEXT NOT NULL,
    "StaffingIntent" TEXT NOT NULL,
    "PreferredExecutorKind" character varying(80) NOT NULL,
    "PreferredProjectAssignmentRole" character varying(64),
    "IsRequired" boolean NOT NULL,
    "AllowsFallback" boolean NOT NULL,
    "RequiresExplicitApproval" boolean NOT NULL,
    "DefaultAllocationPercent" integer NOT NULL,
    "RoleTemplateSourceKey" character varying(160) NOT NULL,
    "RoleTemplateSnapshotName" character varying(200) NOT NULL,
    "SnapshotSummary" TEXT NOT NULL,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_Processes_RoleRequirements" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_RoleSkillRequirements" (
    "Id" uuid NOT NULL,
    "RoleRequirementId" uuid NOT NULL,
    "SkillId" uuid NOT NULL,
    "IsRequired" boolean NOT NULL,
    "MinimumYearsExperience" integer NOT NULL,
    CONSTRAINT "PK_Processes_RoleSkillRequirements" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_RunAssignments" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "RoleRequirementId" uuid NOT NULL,
    "StepDefinitionId" uuid,
    "PartyId" uuid,
    "DisplayName" character varying(200) NOT NULL,
    "ExecutorKind" character varying(80) NOT NULL,
    "BindingReason" TEXT NOT NULL,
    "SourceRegistryKey" character varying(160) NOT NULL,
    "SnapshotSummary" TEXT NOT NULL,
    "IsFallback" boolean NOT NULL,
    "IsCapabilityGap" boolean NOT NULL,
    CONSTRAINT "PK_Processes_RunAssignments" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_Runs" (
    "Id" uuid NOT NULL,
    "ProcessDefinitionId" uuid NOT NULL,
    "ProcessDefinitionVersionId" uuid NOT NULL,
    "ProjectId" uuid,
    "Name" character varying(200) NOT NULL,
    "Status" character varying(48) NOT NULL,
    "OperatingMode" character varying(48) NOT NULL,
    "TriggerReason" TEXT NOT NULL,
    "GovernanceSnapshot" TEXT NOT NULL,
    "PolicySnapshot" TEXT NOT NULL,
    "ExecutorSnapshotSummary" TEXT NOT NULL,
    "ReplayPackageKey" character varying(200) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    "StartedAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "EstimatedCost" numeric NOT NULL,
    "ActualCost" numeric NOT NULL,
    "FirstTimeRightPercent" integer NOT NULL,
    "SlaAttainmentPercent" integer NOT NULL,
    CONSTRAINT "PK_Processes_Runs" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_StepDefinitions" (
    "Id" uuid NOT NULL,
    "ProcessDefinitionVersionId" uuid NOT NULL,
    "Key" character varying(120) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Subtitle" character varying(200) NOT NULL,
    "Notes" TEXT NOT NULL,
    "StepKind" character varying(48) NOT NULL,
    "AllowsManualSkip" boolean NOT NULL,
    "AllowsSafeRefusal" boolean NOT NULL,
    "RequiresApproval" boolean NOT NULL,
    "RequiresDecisionRecord" boolean NOT NULL,
    "InputContractSummary" TEXT NOT NULL,
    "OutputContractSummary" TEXT NOT NULL,
    "EvidenceContractSummary" TEXT NOT NULL,
    "DecisionRightsSummary" TEXT NOT NULL,
    "ExceptionPolicySummary" TEXT NOT NULL,
    "TargetLeadHours" integer NOT NULL,
    "OrderIndex" integer NOT NULL,
    "DependsOnStepId" uuid,
    "CanvasX" double precision NOT NULL,
    "CanvasY" double precision NOT NULL,
    CONSTRAINT "PK_Processes_StepDefinitions" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_StepRoleRequirements" (
    "Id" uuid NOT NULL,
    "StepDefinitionId" uuid NOT NULL,
    "RoleRequirementId" uuid NOT NULL,
    "ResponsibilityKind" character varying(48) NOT NULL,
    "IsRequired" boolean NOT NULL,
    "FallbackOrder" integer NOT NULL,
    "RebindPolicySummary" TEXT NOT NULL,
    CONSTRAINT "PK_Processes_StepRoleRequirements" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_StepRuns" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "StepDefinitionId" uuid NOT NULL,
    "Sequence" integer NOT NULL,
    "Title" character varying(200) NOT NULL,
    "StepKind" character varying(48) NOT NULL,
    "Status" character varying(48) NOT NULL,
    "RoleSnapshotSummary" TEXT NOT NULL,
    "CurrentExecutorName" character varying(200) NOT NULL,
    "CurrentExecutorPartyId" uuid,
    "DecisionSummary" TEXT NOT NULL,
    "BlockedReason" TEXT NOT NULL,
    "RefusalReason" TEXT NOT NULL,
    "ExceptionSummary" TEXT NOT NULL,
    "InputQualitySummary" TEXT NOT NULL,
    "ReadyAtUtc" timestamp with time zone,
    "StartedAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "WaitMinutes" integer NOT NULL,
    "TouchMinutes" integer NOT NULL,
    "BlockedMinutes" integer NOT NULL,
    "ReworkCount" integer NOT NULL,
    "CapabilityGapSeverity" character varying(48) NOT NULL,
    CONSTRAINT "PK_Processes_StepRuns" PRIMARY KEY ("Id")
);

CREATE TABLE "Processes_WorkBriefs" (
    "Id" uuid NOT NULL,
    "ProcessRunId" uuid NOT NULL,
    "StepRunId" uuid,
    "Title" character varying(200) NOT NULL,
    "WorkBriefText" TEXT NOT NULL,
    "HandoffSummary" TEXT NOT NULL,
    "AssignmentReason" TEXT NOT NULL,
    "ExpectedOutcome" TEXT NOT NULL,
    "EvidenceExpectationSummary" TEXT NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_WorkBriefs" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Processes_ArtifactExpectations_StepDefinitionId" ON "Processes_ArtifactExpectations" ("StepDefinitionId");

CREATE INDEX "IX_Processes_ArtifactRecords_ProcessRunId" ON "Processes_ArtifactRecords" ("ProcessRunId");

CREATE INDEX "IX_Processes_ArtifactRecords_StepRunId" ON "Processes_ArtifactRecords" ("StepRunId");

CREATE INDEX "IX_Processes_ConformanceObservations_ProcessRunId" ON "Processes_ConformanceObservations" ("ProcessRunId");

CREATE INDEX "IX_Processes_ConformanceObservations_StepRunId" ON "Processes_ConformanceObservations" ("StepRunId");

CREATE INDEX "IX_Processes_DecisionRecords_ProcessRunId_CreatedAtUtc" ON "Processes_DecisionRecords" ("ProcessRunId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_DecisionRecords_StepRunId" ON "Processes_DecisionRecords" ("StepRunId");

CREATE INDEX "IX_Processes_Definitions_ProjectId" ON "Processes_Definitions" ("ProjectId");

CREATE UNIQUE INDEX "IX_Processes_Definitions_Slug" ON "Processes_Definitions" ("Slug");

CREATE INDEX "IX_Processes_Definitions_Status" ON "Processes_Definitions" ("Status");

CREATE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "Status");

CREATE UNIQUE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNum~" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "VersionNumber");

CREATE INDEX "IX_Processes_ImprovementCandidates_ProcessDefinitionId" ON "Processes_ImprovementCandidates" ("ProcessDefinitionId");

CREATE INDEX "IX_Processes_ImprovementCandidates_ProcessRunId" ON "Processes_ImprovementCandidates" ("ProcessRunId");

CREATE INDEX "IX_Processes_ImprovementCandidates_Status" ON "Processes_ImprovementCandidates" ("Status");

CREATE INDEX "IX_Processes_JournalEntries_ProcessRunId_OccurredAtUtc" ON "Processes_JournalEntries" ("ProcessRunId", "OccurredAtUtc");

CREATE INDEX "IX_Processes_JournalEntries_StepRunId" ON "Processes_JournalEntries" ("StepRunId");

CREATE UNIQUE INDEX "IX_Processes_RoleRequirements_ProcessDefinitionVersionId_Key" ON "Processes_RoleRequirements" ("ProcessDefinitionVersionId", "Key");

CREATE UNIQUE INDEX "IX_Processes_RoleSkillRequirements_RoleRequirementId_SkillId" ON "Processes_RoleSkillRequirements" ("RoleRequirementId", "SkillId");

CREATE INDEX "IX_Processes_RoleSkillRequirements_SkillId" ON "Processes_RoleSkillRequirements" ("SkillId");

CREATE INDEX "IX_Processes_RunAssignments_PartyId" ON "Processes_RunAssignments" ("PartyId");

CREATE INDEX "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_Ste~" ON "Processes_RunAssignments" ("ProcessRunId", "RoleRequirementId", "StepDefinitionId");

CREATE INDEX "IX_Processes_Runs_ProcessDefinitionId" ON "Processes_Runs" ("ProcessDefinitionId");

CREATE INDEX "IX_Processes_Runs_ProjectId" ON "Processes_Runs" ("ProjectId");

CREATE INDEX "IX_Processes_Runs_Status" ON "Processes_Runs" ("Status");

CREATE INDEX "IX_Processes_StepDefinitions_DependsOnStepId" ON "Processes_StepDefinitions" ("DependsOnStepId");

CREATE UNIQUE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_Key" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "Key");

CREATE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_OrderI~" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "OrderIndex");

CREATE UNIQUE INDEX "IX_Processes_StepRoleRequirements_StepDefinitionId_RoleRequire~" ON "Processes_StepRoleRequirements" ("StepDefinitionId", "RoleRequirementId", "ResponsibilityKind");

CREATE UNIQUE INDEX "IX_Processes_StepRuns_ProcessRunId_Sequence" ON "Processes_StepRuns" ("ProcessRunId", "Sequence");

CREATE INDEX "IX_Processes_StepRuns_ProcessRunId_Status" ON "Processes_StepRuns" ("ProcessRunId", "Status");

CREATE INDEX "IX_Processes_StepRuns_StepDefinitionId" ON "Processes_StepRuns" ("StepDefinitionId");

CREATE INDEX "IX_Processes_WorkBriefs_ProcessRunId" ON "Processes_WorkBriefs" ("ProcessRunId");

CREATE INDEX "IX_Processes_WorkBriefs_StepRunId" ON "Processes_WorkBriefs" ("StepRunId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260409104612_AddProcessesFoundation', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Processes_StepRuns" ADD "SelectedBranchOutcomeId" uuid;

ALTER TABLE "Processes_StepRuns" ADD "SelectedBranchOutcomeTitle" character varying(200) NOT NULL DEFAULT '';

ALTER TABLE "Processes_StepDefinitions" ADD "DecisionRoleRequirementId" uuid;

ALTER TABLE "Processes_StepDefinitions" ADD "DependsOnBranchOutcomeId" uuid;

ALTER TABLE "Processes_DecisionRecords" ADD "BranchOutcomeId" uuid;

ALTER TABLE "Processes_DecisionRecords" ADD "BranchOutcomeTitle" character varying(200) NOT NULL DEFAULT '';

CREATE TABLE "Processes_StepBranchOutcomes" (
    "Id" uuid NOT NULL,
    "StepDefinitionId" uuid NOT NULL,
    "Key" character varying(120) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_Processes_StepBranchOutcomes" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Processes_StepRuns_SelectedBranchOutcomeId" ON "Processes_StepRuns" ("SelectedBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDefinitions_DecisionRoleRequirementId" ON "Processes_StepDefinitions" ("DecisionRoleRequirementId");

CREATE INDEX "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId" ON "Processes_StepDefinitions" ("DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_DecisionRecords_BranchOutcomeId" ON "Processes_DecisionRecords" ("BranchOutcomeId");

CREATE INDEX "IX_Processes_StepBranchOutcomes_StepDefinitionId_DisplayOrder" ON "Processes_StepBranchOutcomes" ("StepDefinitionId", "DisplayOrder");

CREATE UNIQUE INDEX "IX_Processes_StepBranchOutcomes_StepDefinitionId_Key" ON "Processes_StepBranchOutcomes" ("StepDefinitionId", "Key");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260410195942_AddProcessBranching', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Processes_StepDefinitions" ADD "BranchCanvasX" double precision NOT NULL DEFAULT 0.0;

ALTER TABLE "Processes_StepDefinitions" ADD "BranchCanvasY" double precision NOT NULL DEFAULT 0.0;

ALTER TABLE "Processes_RoleRequirements" ADD "CanvasX" double precision NOT NULL DEFAULT 0.0;

ALTER TABLE "Processes_RoleRequirements" ADD "CanvasY" double precision NOT NULL DEFAULT 0.0;

CREATE TABLE "Processes_StepDependencies" (
    "Id" uuid NOT NULL,
    "StepDefinitionId" uuid NOT NULL,
    "DependsOnStepId" uuid NOT NULL,
    "DependsOnBranchOutcomeId" uuid,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_Processes_StepDependencies" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Processes_StepDependencies_DependsOnBranchOutcomeId" ON "Processes_StepDependencies" ("DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDependencies_DependsOnStepId" ON "Processes_StepDependencies" ("DependsOnStepId");

CREATE INDEX "IX_Processes_StepDependencies_StepDefinitionId" ON "Processes_StepDependencies" ("StepDefinitionId");

CREATE UNIQUE INDEX "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId~" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDependencies_StepDefinitionId_DisplayOrder" ON "Processes_StepDependencies" ("StepDefinitionId", "DisplayOrder");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260411035512_AddProcessCanvasPositionsAndStepDependencies', '10.0.4');

COMMIT;

START TRANSACTION;
CREATE TABLE "Processes_StepArtifactInputs" (
    "Id" uuid NOT NULL,
    "StepDefinitionId" uuid NOT NULL,
    "ArtifactExpectationId" uuid NOT NULL,
    "DisplayOrder" integer NOT NULL,
    CONSTRAINT "PK_Processes_StepArtifactInputs" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Processes_StepArtifactInputs_ArtifactExpectationId" ON "Processes_StepArtifactInputs" ("ArtifactExpectationId");

CREATE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId" ON "Processes_StepArtifactInputs" ("StepDefinitionId");

CREATE UNIQUE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId_ArtifactExpec~" ON "Processes_StepArtifactInputs" ("StepDefinitionId", "ArtifactExpectationId");

CREATE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId_DisplayOrder" ON "Processes_StepArtifactInputs" ("StepDefinitionId", "DisplayOrder");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260411121334_AddProcessArtifactInputs', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Processes_StepRuns" ADD "ConcurrencyToken" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Processes_Runs" ADD "ConcurrencyToken" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Processes_DefinitionVersions" ADD "ConcurrencyToken" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Processes_Definitions" ADD "ConcurrencyToken" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413101254_AddProcessOptimisticConcurrencyTokens', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Processes_DefinitionVersions" ADD CONSTRAINT "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessD~" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_RoleRequirements" ADD CONSTRAINT "FK_Processes_RoleRequirements_Processes_DefinitionVersions_Pro~" FOREIGN KEY ("ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_RoleSkillRequirements" ADD CONSTRAINT "FK_Processes_RoleSkillRequirements_Processes_RoleRequirements_~" FOREIGN KEY ("RoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepDefinitions" ADD CONSTRAINT "FK_Processes_StepDefinitions_Processes_DefinitionVersions_Proc~" FOREIGN KEY ("ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepDefinitions" ADD CONSTRAINT "FK_Processes_StepDefinitions_Processes_RoleRequirements_Decisi~" FOREIGN KEY ("DecisionRoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413144749_AddProcessDefinitionForeignKeys', '10.0.4');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId~";

DROP INDEX "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId";

DROP INDEX "IX_Processes_StepDefinitions_DependsOnStepId";

ALTER TABLE "Processes_StepDefinitions" DROP COLUMN "DependsOnBranchOutcomeId";

ALTER TABLE "Processes_StepDefinitions" DROP COLUMN "DependsOnStepId";

ALTER TABLE "Processes_DefinitionVersions" ADD CONSTRAINT "AK_Processes_DefinitionVersions_ProcessDefinitionId_Id" UNIQUE ("ProcessDefinitionId", "Id");

CREATE INDEX "IX_Processes_StepRoleRequirements_RoleRequirementId" ON "Processes_StepRoleRequirements" ("RoleRequirementId");

CREATE UNIQUE INDEX "UX_ProcessStepDeps_Conditional" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId") WHERE "DependsOnBranchOutcomeId" IS NOT NULL;

CREATE UNIQUE INDEX "UX_ProcessStepDeps_Unconditional" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId") WHERE "DependsOnBranchOutcomeId" IS NULL;

CREATE INDEX "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersion~" ON "Processes_Runs" ("ProcessDefinitionId", "ProcessDefinitionVersionId");

CREATE INDEX "IX_Processes_RunAssignments_RoleRequirementId" ON "Processes_RunAssignments" ("RoleRequirementId");

CREATE INDEX "IX_Processes_RunAssignments_StepDefinitionId" ON "Processes_RunAssignments" ("StepDefinitionId");

ALTER TABLE "Processes_ArtifactExpectations" ADD CONSTRAINT "FK_Processes_ArtifactExpectations_Processes_StepDefinitions_St~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_ArtifactRecords" ADD CONSTRAINT "FK_Processes_ArtifactRecords_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_ArtifactRecords" ADD CONSTRAINT "FK_Processes_ArtifactRecords_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_ConformanceObservations" ADD CONSTRAINT "FK_Processes_ConformanceObservations_Processes_Runs_ProcessRun~" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_ConformanceObservations" ADD CONSTRAINT "FK_Processes_ConformanceObservations_Processes_StepRuns_StepRu~" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_DecisionRecords" ADD CONSTRAINT "FK_Processes_DecisionRecords_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_DecisionRecords" ADD CONSTRAINT "FK_Processes_DecisionRecords_Processes_StepBranchOutcomes_Bran~" FOREIGN KEY ("BranchOutcomeId") REFERENCES "Processes_StepBranchOutcomes" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_DecisionRecords" ADD CONSTRAINT "FK_Processes_DecisionRecords_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_ImprovementCandidates" ADD CONSTRAINT "FK_Processes_ImprovementCandidates_Processes_Definitions_Proce~" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_ImprovementCandidates" ADD CONSTRAINT "FK_Processes_ImprovementCandidates_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_JournalEntries" ADD CONSTRAINT "FK_Processes_JournalEntries_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_JournalEntries" ADD CONSTRAINT "FK_Processes_JournalEntries_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_RunAssignments" ADD CONSTRAINT "FK_Processes_RunAssignments_Processes_RoleRequirements_RoleReq~" FOREIGN KEY ("RoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_RunAssignments" ADD CONSTRAINT "FK_Processes_RunAssignments_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_RunAssignments" ADD CONSTRAINT "FK_Processes_RunAssignments_Processes_StepDefinitions_StepDefi~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_Runs" ADD CONSTRAINT "FK_Processes_Runs_Processes_DefinitionVersions_ProcessDefiniti~" FOREIGN KEY ("ProcessDefinitionId", "ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("ProcessDefinitionId", "Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_Runs" ADD CONSTRAINT "FK_Processes_Runs_Processes_Definitions_ProcessDefinitionId" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepArtifactInputs" ADD CONSTRAINT "FK_Processes_StepArtifactInputs_Processes_ArtifactExpectations~" FOREIGN KEY ("ArtifactExpectationId") REFERENCES "Processes_ArtifactExpectations" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_StepArtifactInputs" ADD CONSTRAINT "FK_Processes_StepArtifactInputs_Processes_StepDefinitions_Step~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepBranchOutcomes" ADD CONSTRAINT "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepDependencies" ADD CONSTRAINT "FK_Processes_StepDependencies_Processes_StepBranchOutcomes_Dep~" FOREIGN KEY ("DependsOnBranchOutcomeId") REFERENCES "Processes_StepBranchOutcomes" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_StepDependencies" ADD CONSTRAINT "FK_Processes_StepDependencies_Processes_StepDefinitions_Depend~" FOREIGN KEY ("DependsOnStepId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_StepDependencies" ADD CONSTRAINT "FK_Processes_StepDependencies_Processes_StepDefinitions_StepDe~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepRoleRequirements" ADD CONSTRAINT "FK_Processes_StepRoleRequirements_Processes_RoleRequirements_R~" FOREIGN KEY ("RoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_StepRoleRequirements" ADD CONSTRAINT "FK_Processes_StepRoleRequirements_Processes_StepDefinitions_St~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepRuns" ADD CONSTRAINT "FK_Processes_StepRuns_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_StepRuns" ADD CONSTRAINT "FK_Processes_StepRuns_Processes_StepBranchOutcomes_SelectedBra~" FOREIGN KEY ("SelectedBranchOutcomeId") REFERENCES "Processes_StepBranchOutcomes" ("Id") ON DELETE SET NULL;

ALTER TABLE "Processes_StepRuns" ADD CONSTRAINT "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Processes_WorkBriefs" ADD CONSTRAINT "FK_Processes_WorkBriefs_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE;

ALTER TABLE "Processes_WorkBriefs" ADD CONSTRAINT "FK_Processes_WorkBriefs_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413191854_AddProcessRuntimeForeignKeysAndDependencyUniqueness', '10.0.4');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status";

ALTER TABLE "Processes_Definitions" ADD "NextVersionNumber" integer NOT NULL DEFAULT 1;

CREATE UNIQUE INDEX "UX_ProcessVersions_DraftPerDef" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "Status") WHERE "Status" = 'Draft';

CREATE UNIQUE INDEX "UX_ProcessVersions_PubPerDef" ON "Processes_DefinitionVersions" ("ProcessDefinitionId") WHERE "Status" = 'Published';

CREATE INDEX "IX_Processes_Definitions_ActivePublishedVersionId" ON "Processes_Definitions" ("ActivePublishedVersionId");

CREATE INDEX "IX_Processes_Definitions_Id_ActivePublishedVersionId" ON "Processes_Definitions" ("Id", "ActivePublishedVersionId");

ALTER TABLE "Processes_Definitions" ADD CONSTRAINT "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~" FOREIGN KEY ("Id", "ActivePublishedVersionId") REFERENCES "Processes_DefinitionVersions" ("ProcessDefinitionId", "Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413200735_AddProcessDefinitionLifecycleInvariants', '10.0.4');

COMMIT;

START TRANSACTION;
ALTER TABLE "Activity_Entries" ADD "IdempotencyKey" character varying(200);

CREATE TABLE "Processes_Outbox" (
    "Id" uuid NOT NULL,
    "ProjectId" uuid,
    "ProcessDefinitionId" uuid,
    "ProcessRunId" uuid,
    "CommandKey" character varying(120) NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "Status" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "LastAttemptAtUtc" timestamp with time zone,
    "NextAttemptAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "LastError" TEXT NOT NULL,
    "LeaseToken" character varying(100) NOT NULL,
    "LeaseExpiresAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Processes_Outbox" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Activity_Entries_IdempotencyKey" ON "Activity_Entries" ("IdempotencyKey");

CREATE INDEX "IX_Processes_Outbox_ProcessDefinitionId_CreatedAtUtc" ON "Processes_Outbox" ("ProcessDefinitionId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_Outbox_ProcessRunId_CreatedAtUtc" ON "Processes_Outbox" ("ProcessRunId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_Outbox_ProjectId_CreatedAtUtc" ON "Processes_Outbox" ("ProjectId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_Outbox_Status_NextAttemptAtUtc_LeaseExpiresAtUtc" ON "Processes_Outbox" ("Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413204603_AddProcessOutboxDurableSideEffects', '10.0.4');

COMMIT;

