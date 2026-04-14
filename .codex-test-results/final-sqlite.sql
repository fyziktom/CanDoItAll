CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "Activity_Entries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Activity_Entries" PRIMARY KEY,
    "Category" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "ArtifactKind" TEXT NULL,
    "ArtifactId" TEXT NULL,
    "Route" TEXT NULL,
    "Actor" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Factory_PromptBlocks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptBlocks" PRIMARY KEY,
    "Key" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "BlockKind" INTEGER NOT NULL,
    "Summary" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "IsRecommendedByDefault" INTEGER NOT NULL,
    "PromptTypeRules" TEXT NOT NULL,
    "BlueprintRules" TEXT NOT NULL,
    "PhaseRules" TEXT NOT NULL,
    "GroupKey" TEXT NOT NULL,
    "TagsJson" TEXT NOT NULL,
    "StackTagsJson" TEXT NOT NULL,
    "TemplateTokensJson" TEXT NOT NULL,
    "ToolboxEligible" INTEGER NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "CatalogSource" TEXT NOT NULL
);

CREATE TABLE "Factory_PromptBlueprints" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptBlueprints" PRIMARY KEY,
    "Key" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "PromptType" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Guidance" TEXT NOT NULL,
    "RecommendedFlowTemplateId" TEXT NULL,
    "RecommendedFlowKey" TEXT NOT NULL,
    "RecommendedBlockKeysJson" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "CatalogSource" TEXT NOT NULL
);

CREATE TABLE "Factory_PromptBuildSessions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptBuildSessions" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "Phase" TEXT NOT NULL,
    "BlueprintId" TEXT NULL,
    "FlowTemplateId" TEXT NULL,
    "ProviderProfileId" TEXT NULL,
    "PromptArtifactId" TEXT NULL,
    "PromptRunId" TEXT NULL,
    "SelectedPromptRunNodeId" TEXT NULL,
    "RepositoryName" TEXT NOT NULL,
    "BranchName" TEXT NOT NULL,
    "CommitSha" TEXT NOT NULL,
    "SelectedBlockIdsJson" TEXT NOT NULL,
    "SelectedResourceIdsJson" TEXT NOT NULL,
    "GeneratedPrompt" TEXT NOT NULL,
    "WarningSummary" TEXT NOT NULL,
    "CanvasUiStateJson" TEXT NOT NULL,
    "ComponentCustomizationsJson" TEXT NOT NULL,
    "SessionAttachmentsJson" TEXT NOT NULL,
    "WizardStepIndex" INTEGER NOT NULL,
    "HasCustomizedBlocks" INTEGER NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Factory_PromptFlowTemplates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptFlowTemplates" PRIMARY KEY,
    "Key" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "BlockIdsJson" TEXT NOT NULL,
    "BlockKeysJson" TEXT NOT NULL,
    "AgentSequenceJson" TEXT NOT NULL,
    "PromptTypeRules" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "CatalogSource" TEXT NOT NULL
);

CREATE TABLE "Factory_PromptRunNodes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptRunNodes" PRIMARY KEY,
    "PromptRunId" TEXT NOT NULL,
    "PromptBlockDefinitionId" TEXT NULL,
    "PromptArtifactId" TEXT NULL,
    "ParentPromptRunNodeId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "BranchKey" TEXT NOT NULL,
    "BranchLabel" TEXT NOT NULL,
    "Sequence" INTEGER NOT NULL,
    "State" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "Factory_PromptRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Factory_PromptRuns" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "FlowTemplateId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Phase" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Infrastructure_BackgroundJobRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Infrastructure_BackgroundJobRecords" PRIMARY KEY,
    "JobType" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "State" TEXT NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "ErrorSummary" TEXT NULL,
    "CorrelationId" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Infrastructure_SearchDocuments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Infrastructure_SearchDocuments" PRIMARY KEY,
    "SourceType" TEXT NOT NULL,
    "SourceKey" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "Category" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Body" TEXT NOT NULL,
    "Route" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Projects_ProjectHierarchyLinks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_ProjectHierarchyLinks" PRIMARY KEY,
    "ParentProjectId" TEXT NOT NULL,
    "ChildProjectId" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Projects_ProjectOptionSelections" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_ProjectOptionSelections" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "Category" INTEGER NOT NULL,
    "OptionName" TEXT NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "Projects_ProjectPhases" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_ProjectPhases" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Goal" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "StartDateUtc" TEXT NULL,
    "EndDateUtc" TEXT NULL
);

CREATE TABLE "Projects_Projects" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_Projects" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Slug" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Objective" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "CurrentPhase" TEXT NOT NULL,
    "TargetDateUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Prompts_PromptArtifacts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Prompts_PromptArtifacts" PRIMARY KEY,
    "ProjectId" TEXT NULL,
    "CollectionId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "Phase" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "CurrentDraftText" TEXT NOT NULL,
    "CurrentVersionNumber" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Prompts_PromptArtifactTags" (
    "PromptArtifactId" TEXT NOT NULL,
    "PromptTagId" TEXT NOT NULL,
    CONSTRAINT "PK_Prompts_PromptArtifactTags" PRIMARY KEY ("PromptArtifactId", "PromptTagId")
);

CREATE TABLE "Prompts_PromptCollections" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Prompts_PromptCollections" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL
);

CREATE TABLE "Prompts_PromptTags" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Prompts_PromptTags" PRIMARY KEY,
    "Name" TEXT NOT NULL
);

CREATE TABLE "Prompts_PromptUsageRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Prompts_PromptUsageRecords" PRIMARY KEY,
    "PromptArtifactId" TEXT NOT NULL,
    "PromptVersionNumber" INTEGER NULL,
    "ProjectId" TEXT NULL,
    "Phase" TEXT NOT NULL,
    "ProviderName" TEXT NOT NULL,
    "RepositoryName" TEXT NOT NULL,
    "BranchName" TEXT NOT NULL,
    "CommitSha" TEXT NOT NULL,
    "CommitUrl" TEXT NOT NULL,
    "UsageNote" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Prompts_PromptVersions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Prompts_PromptVersions" PRIMARY KEY,
    "PromptArtifactId" TEXT NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    "Content" TEXT NOT NULL,
    "CreationReason" TEXT NOT NULL,
    "OutputFormat" TEXT NOT NULL,
    "SourceBlueprintId" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Resources_ProjectResources" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Resources_ProjectResources" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "ResourceKind" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "LocationOrIdentifier" TEXT NOT NULL,
    "ConfigJson" TEXT NOT NULL,
    "LinkedSecretIdsJson" TEXT NOT NULL,
    "ValidationStatus" INTEGER NOT NULL,
    "Sensitivity" INTEGER NOT NULL,
    "SupportsPreview" INTEGER NOT NULL,
    "SupportsIndexing" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Security_SecretRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Security_SecretRecords" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL,
    "EncryptedPayload" TEXT NOT NULL,
    "Scope" TEXT NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "RotationNote" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Security_SecretReferences" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Security_SecretReferences" PRIMARY KEY,
    "SecretRecordId" TEXT NOT NULL,
    "ContextType" TEXT NOT NULL,
    "ContextId" TEXT NOT NULL,
    "Purpose" TEXT NOT NULL
);

CREATE TABLE "TestLab_TestCases" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TestLab_TestCases" PRIMARY KEY,
    "TestPlanId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "StoryOrFeature" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "TestLab_TestEvidence" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TestLab_TestEvidence" PRIMARY KEY,
    "TestPlanId" TEXT NOT NULL,
    "EvidenceLabel" TEXT NOT NULL,
    "ArtifactPath" TEXT NOT NULL,
    "EvidenceKind" TEXT NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "TestLab_TestPlans" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TestLab_TestPlans" PRIMARY KEY,
    "ProjectId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "Phase" TEXT NOT NULL,
    "CoverageGoal" TEXT NOT NULL,
    "PlaywrightSpecPath" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "TestLab_TestRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TestLab_TestRuns" PRIMARY KEY,
    "TestPlanId" TEXT NOT NULL,
    "ExecutedAtUtc" TEXT NOT NULL,
    "Runner" TEXT NOT NULL,
    "Result" INTEGER NOT NULL,
    "Summary" TEXT NOT NULL
);

CREATE TABLE "Validation_Findings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Validation_Findings" PRIMARY KEY,
    "ValidationRunId" TEXT NOT NULL,
    "RuleCode" TEXT NOT NULL,
    "Severity" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    "Detail" TEXT NOT NULL,
    "RecommendedAction" TEXT NOT NULL
);

CREATE TABLE "Validation_Checklists" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Validation_Checklists" PRIMARY KEY,
    "ValidationType" INTEGER NOT NULL,
    "VersionLabel" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "ItemsJson" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Validation_Runs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Validation_Runs" PRIMARY KEY,
    "ChecklistId" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "ValidationType" INTEGER NOT NULL,
    "ArtifactTitle" TEXT NOT NULL,
    "ArtifactRoute" TEXT NOT NULL,
    "SourceContent" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Decision" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workbench_ProjectObjectLinks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectObjectLinks" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "SourceNodeKey" TEXT NOT NULL,
    "TargetNodeKey" TEXT NOT NULL,
    "LinkKind" INTEGER NOT NULL,
    "IsSystemManaged" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workbench_ProjectObjects" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectObjects" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "NodeKey" TEXT NOT NULL,
    "ObjectType" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    "Subtitle" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "Route" TEXT NOT NULL,
    "ExternalArtifactKind" TEXT NOT NULL,
    "ExternalArtifactId" TEXT NULL,
    "ObjectSubtype" TEXT NOT NULL,
    "MediaRelativePath" TEXT NOT NULL,
    "MediaContentType" TEXT NOT NULL,
    "MediaOriginalFileName" TEXT NOT NULL,
    "ProgressMode" TEXT NOT NULL,
    "ProgressPercent" INTEGER NOT NULL,
    "MarkerIcon" TEXT NOT NULL,
    "MarkerTone" TEXT NOT NULL,
    "MarkerLabel" TEXT NOT NULL,
    "Priority" INTEGER NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "ParentNodeKey" TEXT NULL,
    "PositionX" REAL NOT NULL,
    "PositionY" REAL NOT NULL,
    "StartUtc" TEXT NULL,
    "EndUtc" TEXT NULL,
    "IsSystemManaged" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workbench_ProjectStructureLeases" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectStructureLeases" PRIMARY KEY,
    "ScopeKind" INTEGER NOT NULL,
    "ScopeKey" TEXT NOT NULL,
    "LeaseToken" TEXT NOT NULL,
    "AgentId" TEXT NOT NULL,
    "AgentName" TEXT NOT NULL,
    "MachineName" TEXT NOT NULL,
    "RepositoryRoot" TEXT NOT NULL,
    "BranchName" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "AcquiredAtUtc" TEXT NOT NULL,
    "RenewedAtUtc" TEXT NOT NULL,
    "ExpiresAtUtc" TEXT NOT NULL,
    "ReleasedAtUtc" TEXT NULL
);

CREATE TABLE "Workbench_ProjectStructureOperationAnalytics" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectStructureOperationAnalytics" PRIMARY KEY,
    "OperationName" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "NodeKey" TEXT NULL,
    "ScopeKind" INTEGER NULL,
    "ScopeKey" TEXT NULL,
    "AgentId" TEXT NOT NULL,
    "AgentName" TEXT NOT NULL,
    "MachineName" TEXT NOT NULL,
    "RepositoryRoot" TEXT NOT NULL,
    "BranchName" TEXT NOT NULL,
    "Succeeded" INTEGER NOT NULL,
    "DurationMs" INTEGER NOT NULL,
    "WarningCount" INTEGER NOT NULL,
    "ErrorCode" TEXT NULL,
    "ErrorMessage" TEXT NULL,
    "RequestSummaryJson" TEXT NOT NULL,
    "ResponseSummaryJson" TEXT NOT NULL,
    "WarningsJson" TEXT NOT NULL,
    "OccurredAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workbench_ViewStates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ViewStates" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "SurfaceKind" TEXT NOT NULL,
    "StateJson" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workspace_ProjectStructureAgentProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProjectStructureAgentProfiles" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "AccessTokenCipherText" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "CapabilityMask" INTEGER NOT NULL,
    "AutoApproveMinutes" INTEGER NOT NULL,
    "ApprovalRequiredMinutes" INTEGER NOT NULL,
    "RequireApprovalForAllMutations" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workspace_ProjectStructureAgentProjectOverrides" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProjectStructureAgentProjectOverrides" PRIMARY KEY,
    "ProfileId" TEXT NOT NULL,
    "ProjectId" TEXT NOT NULL,
    "ProjectName" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "CapabilityMask" INTEGER NOT NULL,
    "AutoApproveMinutes" INTEGER NOT NULL,
    "ApprovalRequiredMinutes" INTEGER NOT NULL,
    "RequireApprovalForAllMutations" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "Workspace_ProjectStructureAgentSettings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProjectStructureAgentSettings" PRIMARY KEY,
    "CentralBaseUrl" TEXT NOT NULL,
    "InstallScriptPath" TEXT NOT NULL,
    "SetupReadmePath" TEXT NOT NULL,
    "DefaultAutoApproveMinutes" INTEGER NOT NULL,
    "DefaultApprovalRequiredMinutes" INTEGER NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workspace_ProviderProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProviderProfiles" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "ProviderKind" INTEGER NOT NULL,
    "BaseUrl" TEXT NOT NULL,
    "ApiKeySecretId" TEXT NULL,
    "DefaultModel" TEXT NOT NULL,
    "TimeoutSeconds" INTEGER NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "SupportsStreaming" INTEGER NOT NULL,
    "SupportsToolCalling" INTEGER NOT NULL,
    "SupportsStructuredOutput" INTEGER NOT NULL,
    "SupportsVision" INTEGER NOT NULL,
    "LastHealthCheckAtUtc" TEXT NULL,
    "LastHealthStatus" TEXT NULL,
    "ExtraSettingsJson" TEXT NOT NULL
);

CREATE TABLE "Workspace_Settings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_Settings" PRIMARY KEY,
    "WorkspaceName" TEXT NOT NULL,
    "DefaultProviderProfileId" TEXT NULL,
    "DefaultPromptOutputFormat" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE INDEX "IX_Activity_Entries_CreatedAtUtc" ON "Activity_Entries" ("CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Infrastructure_SearchDocuments_SourceType_SourceKey" ON "Infrastructure_SearchDocuments" ("SourceType", "SourceKey");

CREATE INDEX "IX_Projects_ProjectHierarchyLinks_ChildProjectId" ON "Projects_ProjectHierarchyLinks" ("ChildProjectId");

CREATE INDEX "IX_Projects_ProjectHierarchyLinks_ParentProjectId" ON "Projects_ProjectHierarchyLinks" ("ParentProjectId");

CREATE UNIQUE INDEX "IX_Projects_ProjectHierarchyLinks_ParentProjectId_ChildProjectId" ON "Projects_ProjectHierarchyLinks" ("ParentProjectId", "ChildProjectId");

CREATE INDEX "IX_Projects_ProjectOptionSelections_ProjectId_Category" ON "Projects_ProjectOptionSelections" ("ProjectId", "Category");

CREATE INDEX "IX_Projects_ProjectPhases_ProjectId_OrderIndex" ON "Projects_ProjectPhases" ("ProjectId", "OrderIndex");

CREATE UNIQUE INDEX "IX_Prompts_PromptTags_Name" ON "Prompts_PromptTags" ("Name");

CREATE UNIQUE INDEX "IX_Prompts_PromptVersions_PromptArtifactId_VersionNumber" ON "Prompts_PromptVersions" ("PromptArtifactId", "VersionNumber");

CREATE INDEX "IX_Security_SecretReferences_ContextType_ContextId" ON "Security_SecretReferences" ("ContextType", "ContextId");

CREATE INDEX "IX_Validation_Runs_CreatedAtUtc" ON "Validation_Runs" ("CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Workbench_ProjectObjectLinks_ProjectId_SourceNodeKey_TargetNodeKey_LinkKind_IsSystemManaged" ON "Workbench_ProjectObjectLinks" ("ProjectId", "SourceNodeKey", "TargetNodeKey", "LinkKind", "IsSystemManaged");

CREATE UNIQUE INDEX "IX_Workbench_ProjectObjects_ProjectId_NodeKey" ON "Workbench_ProjectObjects" ("ProjectId", "NodeKey");

CREATE UNIQUE INDEX "IX_Workbench_ProjectStructureLeases_LeaseToken" ON "Workbench_ProjectStructureLeases" ("LeaseToken");

CREATE INDEX "IX_Workbench_ProjectStructureLeases_ScopeKind_ScopeKey" ON "Workbench_ProjectStructureLeases" ("ScopeKind", "ScopeKey");

CREATE INDEX "IX_Workbench_ProjectStructureOperationAnalytics_OccurredAtUtc" ON "Workbench_ProjectStructureOperationAnalytics" ("OccurredAtUtc");

CREATE INDEX "IX_Workbench_ProjectStructureOperationAnalytics_ProjectId_OperationName" ON "Workbench_ProjectStructureOperationAnalytics" ("ProjectId", "OperationName");

CREATE UNIQUE INDEX "IX_Workbench_ViewStates_ProjectId_SurfaceKind" ON "Workbench_ViewStates" ("ProjectId", "SurfaceKind");

CREATE UNIQUE INDEX "IX_Workspace_ProjectStructureAgentProjectOverrides_ProfileId_ProjectId" ON "Workspace_ProjectStructureAgentProjectOverrides" ("ProfileId", "ProjectId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260401094815_InitialCreate', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" ADD "StorageObjectReferenceJson" TEXT NOT NULL DEFAULT '';

CREATE TABLE "Storage_Catalog" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Storage_Catalog" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "ProviderKind" INTEGER NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "IsSystemDefault" INTEGER NOT NULL,
    "IsReadOnly" INTEGER NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "ConnectionMode" INTEGER NOT NULL,
    "EndpointOrRoot" TEXT NOT NULL,
    "ConfigJson" TEXT NOT NULL,
    "CapabilityMask" INTEGER NOT NULL,
    "HealthStatus" INTEGER NOT NULL,
    "LastTestedAtUtc" TEXT NULL,
    "LastHealthMessage" TEXT NOT NULL,
    "CredentialSecretId" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Storage_RoutingRules" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Storage_RoutingRules" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "Priority" INTEGER NOT NULL,
    "ScopeKind" INTEGER NOT NULL,
    "ProjectId" TEXT NULL,
    "NodeKey" TEXT NOT NULL,
    "UsagePurpose" INTEGER NOT NULL,
    "ContentKind" INTEGER NOT NULL,
    "MimePattern" TEXT NOT NULL,
    "MinimumContentLength" INTEGER NULL,
    "MaximumContentLength" INTEGER NULL,
    "EditIntent" INTEGER NOT NULL,
    "PreviewRequired" INTEGER NOT NULL,
    "PublishIntent" INTEGER NOT NULL,
    "RequiredCapabilities" INTEGER NOT NULL,
    "PreferredStorageId" TEXT NOT NULL,
    "AlternativeStorageIdsJson" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_Storage_Catalog_Name" ON "Storage_Catalog" ("Name");

CREATE INDEX "IX_Storage_Catalog_ProviderKind_IsEnabled" ON "Storage_Catalog" ("ProviderKind", "IsEnabled");

CREATE INDEX "IX_Storage_RoutingRules_ScopeKind_ProjectId_NodeKey_Priority_PreferredStorageId" ON "Storage_RoutingRules" ("ScopeKind", "ProjectId", "NodeKey", "Priority", "PreferredStorageId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260402034213_AddStorageFoundation', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" ADD "DurationSeconds" INTEGER NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260402235724_AddProjectObjectDurationSeconds', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "CrmHr_AiAgentProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_AiAgentProfiles" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "ProviderProfileId" TEXT NULL,
    "DefaultModel" TEXT NOT NULL,
    "ExecutionMode" TEXT NOT NULL,
    "OwnerPartyId" TEXT NULL,
    "CapabilityJson" TEXT NOT NULL,
    "ValidationStatus" TEXT NOT NULL,
    "LastReviewedAtUtc" TEXT NULL,
    "Notes" TEXT NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL
);

CREATE TABLE "CrmHr_AuditEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_AuditEntries" PRIMARY KEY,
    "EntityType" TEXT NOT NULL,
    "EntityId" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "DetailJson" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "IsSensitive" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_CapacityBlocks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_CapacityBlocks" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "BlockKind" TEXT NOT NULL,
    "StartDateUtc" TEXT NOT NULL,
    "EndDateUtc" TEXT NOT NULL,
    "Percentage" TEXT NOT NULL,
    "RelatedProjectId" TEXT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_ConfidentialNotes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_ConfidentialNotes" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "NoteText" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_InteractionParties" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_InteractionParties" PRIMARY KEY,
    "InteractionId" TEXT NOT NULL,
    "PartyId" TEXT NOT NULL,
    "Role" TEXT NOT NULL
);

CREATE TABLE "CrmHr_Interactions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_Interactions" PRIMARY KEY,
    "InteractionType" TEXT NOT NULL,
    "Subject" TEXT NOT NULL,
    "OccurredAtUtc" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "NextActionText" TEXT NOT NULL,
    "NextActionOwnerPartyId" TEXT NULL,
    "NextActionDueUtc" TEXT NULL,
    "RelatedOpportunityId" TEXT NULL,
    "RelatedProjectId" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_LookupOptions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_LookupOptions" PRIMARY KEY,
    "CatalogKind" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "IsSystemDefault" INTEGER NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_OnboardingTasks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_OnboardingTasks" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "TaskKind" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "OwnerPartyId" TEXT NULL,
    "DueDateUtc" TEXT NULL,
    "Status" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "RelatedProjectId" TEXT NULL
);

CREATE TABLE "CrmHr_Opportunities" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_Opportunities" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Stage" TEXT NOT NULL,
    "RelationshipStage" TEXT NOT NULL,
    "AccountPartyId" TEXT NOT NULL,
    "OwnerPartyId" TEXT NOT NULL,
    "DeliveryUnitPartyId" TEXT NULL,
    "LinkedProjectId" TEXT NULL,
    "CurrencyCode" TEXT NOT NULL,
    "Amount" TEXT NULL,
    "ProbabilityPercent" INTEGER NOT NULL,
    "ExpectedCloseDateUtc" TEXT NULL,
    "OpportunitySource" TEXT NOT NULL,
    "LostReason" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_OpportunityParties" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_OpportunityParties" PRIMARY KEY,
    "OpportunityId" TEXT NOT NULL,
    "PartyId" TEXT NOT NULL,
    "Role" TEXT NOT NULL
);

CREATE TABLE "CrmHr_OpportunityStageHistory" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_OpportunityStageHistory" PRIMARY KEY,
    "OpportunityId" TEXT NOT NULL,
    "Stage" TEXT NOT NULL,
    "ChangedAtUtc" TEXT NOT NULL,
    "ChangedBy" TEXT NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_Parties" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_Parties" PRIMARY KEY,
    "PartyType" TEXT NOT NULL,
    "LifecycleStatus" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "LegalName" TEXT NOT NULL,
    "PreferredName" TEXT NOT NULL,
    "ExternalCode" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "TagsJson" TEXT NOT NULL,
    "Region" TEXT NOT NULL,
    "CountryCode" TEXT NOT NULL,
    "TimeZone" TEXT NOT NULL,
    "IsSensitive" INTEGER NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    "LastChangedBy" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_PartyAddresses" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_PartyAddresses" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "AddressType" TEXT NOT NULL,
    "Line1" TEXT NOT NULL,
    "Line2" TEXT NOT NULL,
    "City" TEXT NOT NULL,
    "Region" TEXT NOT NULL,
    "PostalCode" TEXT NOT NULL,
    "CountryCode" TEXT NOT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_PartyContactPoints" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_PartyContactPoints" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "ContactType" TEXT NOT NULL,
    "Label" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    "NormalizedValue" TEXT NOT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "IsPublic" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_PartyRelationships" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_PartyRelationships" PRIMARY KEY,
    "SourcePartyId" TEXT NOT NULL,
    "TargetPartyId" TEXT NOT NULL,
    "RelationshipKind" TEXT NOT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "StartDateUtc" TEXT NULL,
    "EndDateUtc" TEXT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_PartyRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_PartyRoles" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "RoleKind" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "ValidFromUtc" TEXT NULL,
    "ValidToUtc" TEXT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_PartySkills" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_PartySkills" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "SkillId" TEXT NOT NULL,
    "Proficiency" TEXT NOT NULL,
    "YearsExperience" INTEGER NOT NULL,
    "CertificationStatus" TEXT NOT NULL,
    "LastValidatedAtUtc" TEXT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_ProjectPartyAssignments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_ProjectPartyAssignments" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "PartyId" TEXT NOT NULL,
    "AssignmentKind" TEXT NOT NULL,
    "NodeKey" TEXT NOT NULL,
    "PhaseName" TEXT NOT NULL,
    "OpportunityId" TEXT NULL,
    "AllocationPercent" TEXT NULL,
    "StartsAtUtc" TEXT NULL,
    "EndsAtUtc" TEXT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "Source" TEXT NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_RecruitmentApplications" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_RecruitmentApplications" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "TargetUnitPartyId" TEXT NULL,
    "RecruiterPartyId" TEXT NULL,
    "HiringManagerPartyId" TEXT NULL,
    "DesiredRole" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "Stage" TEXT NOT NULL,
    "AvailableFromUtc" TEXT NULL,
    "Decision" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_RecruitmentInterviews" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_RecruitmentInterviews" PRIMARY KEY,
    "ApplicationId" TEXT NOT NULL,
    "ScheduledAtUtc" TEXT NOT NULL,
    "InterviewType" TEXT NOT NULL,
    "InterviewerPartyId" TEXT NULL,
    "Outcome" TEXT NOT NULL,
    "Feedback" TEXT NOT NULL,
    "Recommendation" TEXT NOT NULL
);

CREATE TABLE "CrmHr_Skills" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_Skills" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL
);

CREATE TABLE "CrmHr_StaffingRequests" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_StaffingRequests" PRIMARY KEY,
    "ProjectId" TEXT NULL,
    "RequestedByPartyId" TEXT NULL,
    "DeliveryUnitPartyId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "NeededRole" TEXT NOT NULL,
    "NeededSkillsJson" TEXT NOT NULL,
    "StartDateUtc" TEXT NULL,
    "EndDateUtc" TEXT NULL,
    "AllocationPercent" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Notes" TEXT NOT NULL
);

CREATE TABLE "CrmHr_WorkforceProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_WorkforceProfiles" PRIMARY KEY,
    "PartyId" TEXT NOT NULL,
    "WorkforceKind" TEXT NOT NULL,
    "EmployeeCode" TEXT NOT NULL,
    "JobTitle" TEXT NOT NULL,
    "Discipline" TEXT NOT NULL,
    "Seniority" TEXT NOT NULL,
    "HomeUnitPartyId" TEXT NULL,
    "ManagerPartyId" TEXT NULL,
    "StartDateUtc" TEXT NULL,
    "EndDateUtc" TEXT NULL,
    "Location" TEXT NOT NULL,
    "TimeZone" TEXT NOT NULL,
    "InternalCostRate" TEXT NULL,
    "ExternalBillingRate" TEXT NULL,
    "CapacityHoursPerWeek" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "ExtendedDataJson" TEXT NOT NULL,
    "Notes" TEXT NOT NULL
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

CREATE INDEX "IX_CrmHr_PartyRelationships_SourcePartyId_TargetPartyId_RelationshipKind" ON "CrmHr_PartyRelationships" ("SourcePartyId", "TargetPartyId", "RelationshipKind");

CREATE INDEX "IX_CrmHr_PartyRelationships_TargetPartyId" ON "CrmHr_PartyRelationships" ("TargetPartyId");

CREATE INDEX "IX_CrmHr_PartyRoles_PartyId_RoleKind" ON "CrmHr_PartyRoles" ("PartyId", "RoleKind");

CREATE UNIQUE INDEX "IX_CrmHr_PartySkills_PartyId_SkillId" ON "CrmHr_PartySkills" ("PartyId", "SkillId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_OpportunityId" ON "CrmHr_ProjectPartyAssignments" ("OpportunityId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_PartyId" ON "CrmHr_ProjectPartyAssignments" ("PartyId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_ProjectId" ON "CrmHr_ProjectPartyAssignments" ("ProjectId");

CREATE INDEX "IX_CrmHr_ProjectPartyAssignments_ProjectId_PartyId_AssignmentKind_NodeKey" ON "CrmHr_ProjectPartyAssignments" ("ProjectId", "PartyId", "AssignmentKind", "NodeKey");

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
VALUES ('20260403170902_AddCrmHrFoundation', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "CrmHr_AccountProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_AccountProfiles" PRIMARY KEY,
    "AccountPartyId" TEXT NOT NULL,
    "RelationshipStage" TEXT NOT NULL,
    "CommercialNotes" TEXT NOT NULL,
    "ConstraintNotes" TEXT NOT NULL,
    "TimingRiskNotes" TEXT NOT NULL,
    "LastChangedBy" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "CrmHr_AccountStakeholders" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CrmHr_AccountStakeholders" PRIMARY KEY,
    "AccountPartyId" TEXT NOT NULL,
    "RelatedPartyId" TEXT NOT NULL,
    "Role" TEXT NOT NULL,
    "IsPrimary" INTEGER NOT NULL,
    "Notes" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_CrmHr_AccountProfiles_AccountPartyId" ON "CrmHr_AccountProfiles" ("AccountPartyId");

CREATE UNIQUE INDEX "IX_CrmHr_AccountStakeholders_AccountPartyId_RelatedPartyId_Role" ON "CrmHr_AccountStakeholders" ("AccountPartyId", "RelatedPartyId", "Role");

CREATE INDEX "IX_CrmHr_AccountStakeholders_RelatedPartyId" ON "CrmHr_AccountStakeholders" ("RelatedPartyId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260403194503_AddCrmHrAccountsAndInteractions', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Validation_Runs" ADD "ResponsiblePartyId" TEXT NULL;

ALTER TABLE "TestLab_TestPlans" ADD "ResponsiblePartyId" TEXT NULL;

ALTER TABLE "Resources_ProjectResources" ADD "MaintainerPartyId" TEXT NULL;

ALTER TABLE "Resources_ProjectResources" ADD "OwnerPartyId" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260404044417_AddCrmHrCrossModuleResponsibleParties', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "Workbench_ProjectProjectionLayouts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectProjectionLayouts" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "NodeKey" TEXT NOT NULL,
    "PositionX" REAL NOT NULL,
    "PositionY" REAL NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_Workbench_ProjectProjectionLayouts_ProjectId_NodeKey" ON "Workbench_ProjectProjectionLayouts" ("ProjectId", "NodeKey");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405021055_AddWorkbenchProjectionLayouts', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "Workbench_ProjectNodeBindings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeBindings" PRIMARY KEY,
    "ProjectObjectId" TEXT NOT NULL,
    "Route" TEXT NOT NULL,
    "ExternalArtifactKind" TEXT NOT NULL,
    "ExternalArtifactId" TEXT NULL,
    "MediaRelativePath" TEXT NOT NULL,
    "MediaContentType" TEXT NOT NULL,
    "MediaOriginalFileName" TEXT NOT NULL,
    "StorageObjectReferenceJson" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Workbench_ProjectNodeBindings_Workbench_ProjectObjects_ProjectObjectId" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Workbench_ProjectNodeReferences" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeReferences" PRIMARY KEY,
    "ProjectObjectId" TEXT NOT NULL,
    "ReferenceKind" INTEGER NOT NULL,
    "ReferenceId" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Workbench_ProjectNodeReferences_Workbench_ProjectObjects_ProjectObjectId" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Workbench_ProjectNodeBindings_ProjectObjectId" ON "Workbench_ProjectNodeBindings" ("ProjectObjectId");

CREATE INDEX "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKind_OrderIndex" ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "OrderIndex");

CREATE UNIQUE INDEX "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKind_ReferenceId" ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "ReferenceId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405024055_AddProjectNodeBindings', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "Workbench_ProjectNodeLifecycleEvents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeLifecycleEvents" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "ProjectObjectId" TEXT NOT NULL,
    "NodeKey" TEXT NOT NULL,
    "TransitionMode" INTEGER NOT NULL,
    "SourceFamily" INTEGER NOT NULL,
    "TargetFamily" INTEGER NOT NULL,
    "SourceObjectType" INTEGER NOT NULL,
    "SourceObjectSubtype" TEXT NOT NULL,
    "TargetObjectType" INTEGER NOT NULL,
    "TargetObjectSubtype" TEXT NOT NULL,
    "SourceSnapshotJson" TEXT NOT NULL,
    "TargetSnapshotJson" TEXT NOT NULL,
    "OccurredAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Workbench_ProjectNodeLifecycleEvents_Workbench_ProjectObjects_ProjectObjectId" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Workbench_ProjectNodeLifecycleEvents_ProjectId_NodeKey_OccurredAtUtc" ON "Workbench_ProjectNodeLifecycleEvents" ("ProjectId", "NodeKey", "OccurredAtUtc");

CREATE INDEX "IX_Workbench_ProjectNodeLifecycleEvents_ProjectObjectId" ON "Workbench_ProjectNodeLifecycleEvents" ("ProjectObjectId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405033221_AddProjectNodeLifecycleEvents', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Workspace_ProviderProfiles" ADD "ConfigSchemaVersion" TEXT NOT NULL DEFAULT '';

ALTER TABLE "Workspace_ProviderProfiles" ADD "ConnectorPluginKey" TEXT NOT NULL DEFAULT '';

ALTER TABLE "Resources_ProjectResources" ADD "ConfigSchemaVersion" TEXT NOT NULL DEFAULT '';

ALTER TABLE "Resources_ProjectResources" ADD "ConnectorPluginKey" TEXT NOT NULL DEFAULT '';

CREATE TABLE "Workbench_ProjectCrossModuleMutations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectCrossModuleMutations" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "ScopeNodeKey" TEXT NOT NULL,
    "MutationKind" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "ErrorMessage" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE INDEX "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ScopeNodeKey_CreatedAtUtc" ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ScopeNodeKey", "CreatedAtUtc");

CREATE INDEX "IX_Workbench_ProjectCrossModuleMutations_ProjectId_Status_UpdatedAtUtc" ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "Status", "UpdatedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405041730_AddConnectorPluginPlatformAndCrossModuleMutations', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Workbench_ProjectObjects" ADD "MarkersJson" TEXT NOT NULL DEFAULT '[]';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405150244_AddProjectObjectMarkersJson', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "ApprovalState" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "AttemptCount" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "CompletedAtUtc" TEXT NULL;

ALTER TABLE "Workbench_ProjectCrossModuleMutations" ADD "LastAttemptAtUtc" TEXT NULL;

CREATE INDEX "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalState_Status_UpdatedAtUtc" ON "Workbench_ProjectCrossModuleMutations" ("ProjectId", "ApprovalState", "Status", "UpdatedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405194312_AddCrossModuleMutationDurabilityFields', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "Workspace_ConnectorCommands" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ConnectorCommands" PRIMARY KEY,
    "ProjectId" TEXT NOT NULL,
    "ConnectorPluginKey" TEXT NOT NULL,
    "CommandKey" TEXT NOT NULL,
    "IdempotencyKey" TEXT NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "ApprovalState" INTEGER NOT NULL,
    "AttemptCount" INTEGER NOT NULL,
    "LastAttemptAtUtc" TEXT NULL,
    "NextAttemptAtUtc" TEXT NULL,
    "CompletedAtUtc" TEXT NULL,
    "LastError" TEXT NOT NULL,
    "ResultJson" TEXT NOT NULL,
    "RequestedBy" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Workspace_ConnectorCommandAudits" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ConnectorCommandAudits" PRIMARY KEY,
    "ConnectorCommandId" TEXT NOT NULL,
    "ProjectId" TEXT NOT NULL,
    "EventKind" INTEGER NOT NULL,
    "Actor" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "DetailsJson" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Workspace_ConnectorCommandAudits_Workspace_ConnectorCommands_ConnectorCommandId" FOREIGN KEY ("ConnectorCommandId") REFERENCES "Workspace_ConnectorCommands" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_CreatedAtUtc" ON "Workspace_ConnectorCommandAudits" ("ConnectorCommandId", "CreatedAtUtc");

CREATE UNIQUE INDEX "IX_Workspace_ConnectorCommands_ProjectId_ConnectorPluginKey_CommandKey_IdempotencyKey" ON "Workspace_ConnectorCommands" ("ProjectId", "ConnectorPluginKey", "CommandKey", "IdempotencyKey");

CREATE INDEX "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc" ON "Workspace_ConnectorCommands" ("ProjectId", "CreatedAtUtc");

CREATE INDEX "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc" ON "Workspace_ConnectorCommands" ("Status", "ApprovalState", "NextAttemptAtUtc");

CREATE TABLE "ef_temp_Workbench_ProjectObjects" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectObjects" PRIMARY KEY,
    "CreatedAtUtc" TEXT NOT NULL,
    "DurationSeconds" INTEGER NULL,
    "EndUtc" TEXT NULL,
    "IsSystemManaged" INTEGER NOT NULL,
    "MarkersJson" TEXT NOT NULL,
    "MetadataJson" TEXT NOT NULL,
    "NodeKey" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "ObjectSubtype" TEXT NOT NULL,
    "ObjectType" INTEGER NOT NULL,
    "ParentNodeKey" TEXT NULL,
    "PositionX" REAL NOT NULL,
    "PositionY" REAL NOT NULL,
    "Priority" INTEGER NOT NULL,
    "ProgressMode" TEXT NOT NULL,
    "ProgressPercent" INTEGER NOT NULL,
    "ProjectId" TEXT NOT NULL,
    "StartUtc" TEXT NULL,
    "Status" TEXT NOT NULL,
    "Subtitle" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

INSERT INTO "ef_temp_Workbench_ProjectObjects" ("Id", "CreatedAtUtc", "DurationSeconds", "EndUtc", "IsSystemManaged", "MarkersJson", "MetadataJson", "NodeKey", "Notes", "ObjectSubtype", "ObjectType", "ParentNodeKey", "PositionX", "PositionY", "Priority", "ProgressMode", "ProgressPercent", "ProjectId", "StartUtc", "Status", "Subtitle", "Title", "UpdatedAtUtc")
SELECT "Id", "CreatedAtUtc", "DurationSeconds", "EndUtc", "IsSystemManaged", "MarkersJson", "MetadataJson", "NodeKey", "Notes", "ObjectSubtype", "ObjectType", "ParentNodeKey", "PositionX", "PositionY", "Priority", "ProgressMode", "ProgressPercent", "ProjectId", "StartUtc", "Status", "Subtitle", "Title", "UpdatedAtUtc"
FROM "Workbench_ProjectObjects";

CREATE TABLE "ef_temp_Workspace_ProviderProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workspace_ProviderProfiles" PRIMARY KEY,
    "ApiKeySecretId" TEXT NULL,
    "BaseUrl" TEXT NOT NULL,
    "ConfigSchemaVersion" TEXT NOT NULL,
    "ConnectorPluginKey" TEXT NOT NULL,
    "DefaultModel" TEXT NOT NULL,
    "ExtraSettingsJson" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "LastHealthCheckAtUtc" TEXT NULL,
    "LastHealthStatus" TEXT NULL,
    "Name" TEXT NOT NULL,
    "ProviderKind" INTEGER NULL,
    "SupportsStreaming" INTEGER NOT NULL,
    "SupportsStructuredOutput" INTEGER NOT NULL,
    "SupportsToolCalling" INTEGER NOT NULL,
    "SupportsVision" INTEGER NOT NULL,
    "TimeoutSeconds" INTEGER NOT NULL
);

INSERT INTO "ef_temp_Workspace_ProviderProfiles" ("Id", "ApiKeySecretId", "BaseUrl", "ConfigSchemaVersion", "ConnectorPluginKey", "DefaultModel", "ExtraSettingsJson", "IsEnabled", "LastHealthCheckAtUtc", "LastHealthStatus", "Name", "ProviderKind", "SupportsStreaming", "SupportsStructuredOutput", "SupportsToolCalling", "SupportsVision", "TimeoutSeconds")
SELECT "Id", "ApiKeySecretId", "BaseUrl", "ConfigSchemaVersion", "ConnectorPluginKey", "DefaultModel", "ExtraSettingsJson", "IsEnabled", "LastHealthCheckAtUtc", "LastHealthStatus", "Name", "ProviderKind", "SupportsStreaming", "SupportsStructuredOutput", "SupportsToolCalling", "SupportsVision", "TimeoutSeconds"
FROM "Workspace_ProviderProfiles";

CREATE TABLE "ef_temp_Workbench_ProjectNodeReferences" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Workbench_ProjectNodeReferences" PRIMARY KEY,
    "CreatedAtUtc" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "ProjectObjectId" TEXT NOT NULL,
    "ReferenceId" TEXT NOT NULL,
    "ReferenceKind" TEXT NOT NULL,
    CONSTRAINT "FK_Workbench_ProjectNodeReferences_Workbench_ProjectObjects_ProjectObjectId" FOREIGN KEY ("ProjectObjectId") REFERENCES "Workbench_ProjectObjects" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Workbench_ProjectNodeReferences" ("Id", "CreatedAtUtc", "OrderIndex", "ProjectObjectId", "ReferenceId", "ReferenceKind")
SELECT "Id", "CreatedAtUtc", "OrderIndex", "ProjectObjectId", "ReferenceId", "ReferenceKind"
FROM "Workbench_ProjectNodeReferences";

CREATE TABLE "ef_temp_Resources_ProjectResources" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Resources_ProjectResources" PRIMARY KEY,
    "ConfigJson" TEXT NOT NULL,
    "ConfigSchemaVersion" TEXT NOT NULL,
    "ConnectorPluginKey" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "LinkedSecretIdsJson" TEXT NOT NULL,
    "LocationOrIdentifier" TEXT NOT NULL,
    "MaintainerPartyId" TEXT NULL,
    "Name" TEXT NOT NULL,
    "OwnerPartyId" TEXT NULL,
    "ProjectId" TEXT NOT NULL,
    "ResourceKind" INTEGER NULL,
    "Sensitivity" INTEGER NOT NULL,
    "SupportsIndexing" INTEGER NOT NULL,
    "SupportsPreview" INTEGER NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "ValidationStatus" INTEGER NOT NULL
);

INSERT INTO "ef_temp_Resources_ProjectResources" ("Id", "ConfigJson", "ConfigSchemaVersion", "ConnectorPluginKey", "CreatedAtUtc", "Description", "LinkedSecretIdsJson", "LocationOrIdentifier", "MaintainerPartyId", "Name", "OwnerPartyId", "ProjectId", "ResourceKind", "Sensitivity", "SupportsIndexing", "SupportsPreview", "UpdatedAtUtc", "ValidationStatus")
SELECT "Id", "ConfigJson", "ConfigSchemaVersion", "ConnectorPluginKey", "CreatedAtUtc", "Description", "LinkedSecretIdsJson", "LocationOrIdentifier", "MaintainerPartyId", "Name", "OwnerPartyId", "ProjectId", "ResourceKind", "Sensitivity", "SupportsIndexing", "SupportsPreview", "UpdatedAtUtc", "ValidationStatus"
FROM "Resources_ProjectResources";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Workbench_ProjectObjects";

ALTER TABLE "ef_temp_Workbench_ProjectObjects" RENAME TO "Workbench_ProjectObjects";

DROP TABLE "Workspace_ProviderProfiles";

ALTER TABLE "ef_temp_Workspace_ProviderProfiles" RENAME TO "Workspace_ProviderProfiles";

DROP TABLE "Workbench_ProjectNodeReferences";

ALTER TABLE "ef_temp_Workbench_ProjectNodeReferences" RENAME TO "Workbench_ProjectNodeReferences";

DROP TABLE "Resources_ProjectResources";

ALTER TABLE "ef_temp_Resources_ProjectResources" RENAME TO "Resources_ProjectResources";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE UNIQUE INDEX "IX_Workbench_ProjectObjects_ProjectId_NodeKey" ON "Workbench_ProjectObjects" ("ProjectId", "NodeKey");

CREATE INDEX "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKind_OrderIndex" ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "OrderIndex");

CREATE UNIQUE INDEX "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKind_ReferenceId" ON "Workbench_ProjectNodeReferences" ("ProjectObjectId", "ReferenceKind", "ReferenceId");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260405222845_AddConnectorCommandOutboxBoundary', '10.0.4');

BEGIN TRANSACTION;
CREATE TABLE "Automation_DeadLetters" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_DeadLetters" PRIMARY KEY,
    "EnvelopeId" TEXT NOT NULL,
    "DeliveryId" TEXT NOT NULL,
    "EnvelopeType" TEXT NOT NULL,
    "HandlerKey" TEXT NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "ErrorMessage" TEXT NOT NULL,
    "AttemptCount" INTEGER NOT NULL,
    "DedupeKey" TEXT NULL,
    "CorrelationId" TEXT NULL,
    "CausationId" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "DeadLetteredAtUtc" TEXT NOT NULL
);

CREATE TABLE "Automation_DeliveryAttempts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_DeliveryAttempts" PRIMARY KEY,
    "EnvelopeId" TEXT NOT NULL,
    "DeliveryId" TEXT NOT NULL,
    "HandlerKey" TEXT NOT NULL,
    "AttemptNumber" INTEGER NOT NULL,
    "Outcome" INTEGER NOT NULL,
    "CorrelationId" TEXT NULL,
    "CausationId" TEXT NULL,
    "ErrorMessage" TEXT NOT NULL,
    "StartedAtUtc" TEXT NOT NULL,
    "CompletedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Automation_Envelopes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_Envelopes" PRIMARY KEY,
    "EnvelopeType" TEXT NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "State" INTEGER NOT NULL,
    "AttemptCount" INTEGER NOT NULL,
    "DedupeKey" TEXT NULL,
    "CorrelationId" TEXT NULL,
    "CausationId" TEXT NULL,
    "AvailableAtUtc" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "CompletedAtUtc" TEXT NULL
);

CREATE TABLE "Automation_ExecutionLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_ExecutionLogs" PRIMARY KEY,
    "EventKind" INTEGER NOT NULL,
    "SourceType" TEXT NOT NULL,
    "SourceId" TEXT NOT NULL,
    "CorrelationId" TEXT NULL,
    "CausationId" TEXT NULL,
    "Message" TEXT NOT NULL,
    "DetailsJson" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Automation_PluginIngressCursors" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_PluginIngressCursors" PRIMARY KEY,
    "SourceKind" TEXT NOT NULL,
    "SourceKey" TEXT NOT NULL,
    "CursorValue" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Automation_PluginIngressEnvelopes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_PluginIngressEnvelopes" PRIMARY KEY,
    "SourceKind" TEXT NOT NULL,
    "SourceKey" TEXT NOT NULL,
    "ExternalMessageId" TEXT NOT NULL,
    "CursorValue" TEXT NOT NULL,
    "DedupeKey" TEXT NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "State" INTEGER NOT NULL,
    "CorrelationId" TEXT NULL,
    "MaterializerKey" TEXT NOT NULL,
    "MaterializationSummary" TEXT NOT NULL,
    "LastError" TEXT NOT NULL,
    "ReceivedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "MaterializedAtUtc" TEXT NULL
);

CREATE TABLE "Automation_Triggers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_Triggers" PRIMARY KEY,
    "OwnerKind" INTEGER NOT NULL,
    "OwnerKey" TEXT NOT NULL,
    "TriggerKey" TEXT NOT NULL,
    "IsEnabled" INTEGER NOT NULL,
    "TriggerKind" INTEGER NOT NULL,
    "CronExpression" TEXT NOT NULL,
    "TimeZoneId" TEXT NOT NULL,
    "StartAtUtc" TEXT NULL,
    "EndAtUtc" TEXT NULL,
    "MisfirePolicy" INTEGER NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "DedupeKey" TEXT NOT NULL,
    "NextPlannedFireAtUtc" TEXT NULL,
    "LastFiredAtUtc" TEXT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Automation_EnvelopeDeliveries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Automation_EnvelopeDeliveries" PRIMARY KEY,
    "EnvelopeId" TEXT NOT NULL,
    "EnvelopeType" TEXT NOT NULL,
    "HandlerKey" TEXT NOT NULL,
    "State" INTEGER NOT NULL,
    "AttemptCount" INTEGER NOT NULL,
    "MaxAttempts" INTEGER NOT NULL,
    "AvailableAtUtc" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "LastAttemptAtUtc" TEXT NULL,
    "CompletedAtUtc" TEXT NULL,
    "LastError" TEXT NOT NULL,
    "LockToken" TEXT NOT NULL,
    "LockedAtUtc" TEXT NULL,
    CONSTRAINT "FK_Automation_EnvelopeDeliveries_Automation_Envelopes_EnvelopeId" FOREIGN KEY ("EnvelopeId") REFERENCES "Automation_Envelopes" ("Id") ON DELETE CASCADE
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

CREATE UNIQUE INDEX "IX_Automation_PluginIngressEnvelopes_SourceKind_SourceKey_DedupeKey" ON "Automation_PluginIngressEnvelopes" ("SourceKind", "SourceKey", "DedupeKey");

CREATE INDEX "IX_Automation_PluginIngressEnvelopes_State_ReceivedAtUtc" ON "Automation_PluginIngressEnvelopes" ("State", "ReceivedAtUtc");

CREATE UNIQUE INDEX "IX_Automation_Triggers_OwnerKind_OwnerKey_TriggerKey" ON "Automation_Triggers" ("OwnerKind", "OwnerKey", "TriggerKey");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260406023416_AddAutomationRuntimePlane', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
DROP INDEX "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc";

DROP INDEX "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc";

ALTER TABLE "Workspace_ConnectorCommands" ADD "LeaseExpiresAtUtc" TEXT NULL;

ALTER TABLE "Workspace_ConnectorCommands" ADD "LeaseToken" TEXT NOT NULL DEFAULT '';

CREATE INDEX "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc_LeaseExpiresAtUtc" ON "Workspace_ConnectorCommands" ("Status", "ApprovalState", "NextAttemptAtUtc", "LeaseExpiresAtUtc");

CREATE INDEX "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc_LockedAtUtc" ON "Automation_EnvelopeDeliveries" ("State", "AvailableAtUtc", "LockedAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260406144904_AddAutomationRuntimeHardeningPhase13', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "Processes_ArtifactExpectations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ArtifactExpectations" PRIMARY KEY,
    "StepDefinitionId" TEXT NOT NULL,
    "ArtifactKind" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    "TrustRequirement" TEXT NOT NULL,
    "SensitivityLevel" TEXT NOT NULL,
    "RetentionDays" INTEGER NOT NULL,
    "AllowedFutureUsageSummary" TEXT NOT NULL,
    "ValidationRequirementSummary" TEXT NOT NULL
);

CREATE TABLE "Processes_ArtifactRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ArtifactRecords" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "ArtifactKind" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "TrustStatus" TEXT NOT NULL,
    "SensitivityLevel" TEXT NOT NULL,
    "ProvenanceSummary" TEXT NOT NULL,
    "AllowedFutureUsageSummary" TEXT NOT NULL,
    "ReviewSummary" TEXT NOT NULL,
    "ManagedStoragePath" TEXT NOT NULL,
    "ExternalReferenceKey" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Processes_ConformanceObservations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ConformanceObservations" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "Severity" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Observation" TEXT NOT NULL,
    "DeviationReason" TEXT NOT NULL,
    "IsSafeNonAction" INTEGER NOT NULL,
    "ContainsSensitiveAssessment" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Processes_DecisionRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_DecisionRecords" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "DecisionKind" TEXT NOT NULL,
    "Outcome" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "PolicyEvaluation" TEXT NOT NULL,
    "DecidedBy" TEXT NOT NULL,
    "OperatingMode" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Processes_Definitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_Definitions" PRIMARY KEY,
    "ProjectId" TEXT NULL,
    "Name" TEXT NOT NULL,
    "Slug" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "ValueStatement" TEXT NOT NULL,
    "CustomerName" TEXT NOT NULL,
    "OwnerName" TEXT NOT NULL,
    "InterfaceContractSummary" TEXT NOT NULL,
    "GovernanceNotes" TEXT NOT NULL,
    "Criticality" TEXT NOT NULL,
    "AutonomyLevel" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "ActivePublishedVersionId" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE TABLE "Processes_DefinitionVersions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_DefinitionVersions" PRIMARY KEY,
    "ProcessDefinitionId" TEXT NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "ChangeSummary" TEXT NOT NULL,
    "GovernancePolicySummary" TEXT NOT NULL,
    "ConstitutionRuleSummary" TEXT NOT NULL,
    "OperatingModeSummary" TEXT NOT NULL,
    "SimulationReadinessSummary" TEXT NOT NULL,
    "ImportedFrom" TEXT NOT NULL,
    "ImportWarnings" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "PublishedAtUtc" TEXT NULL,
    "PublishedBy" TEXT NOT NULL
);

CREATE TABLE "Processes_ImprovementCandidates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ImprovementCandidates" PRIMARY KEY,
    "ProcessDefinitionId" TEXT NOT NULL,
    "ProcessRunId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "ProblemSummary" TEXT NOT NULL,
    "EvidenceSummary" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "IsTrainingOpportunity" INTEGER NOT NULL,
    "RequiresGovernanceReview" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "ClosedAtUtc" TEXT NULL
);

CREATE TABLE "Processes_JournalEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_JournalEntries" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "EventType" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "CorrelationId" TEXT NOT NULL,
    "OperatingMode" TEXT NOT NULL,
    "PolicyVersion" TEXT NOT NULL,
    "EnvironmentMode" TEXT NOT NULL,
    "ReplayContextJson" TEXT NOT NULL,
    "OccurredAtUtc" TEXT NOT NULL
);

CREATE TABLE "Processes_RoleRequirements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_RoleRequirements" PRIMARY KEY,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "Purpose" TEXT NOT NULL,
    "StaffingIntent" TEXT NOT NULL,
    "PreferredExecutorKind" TEXT NOT NULL,
    "PreferredProjectAssignmentRole" TEXT NULL,
    "IsRequired" INTEGER NOT NULL,
    "AllowsFallback" INTEGER NOT NULL,
    "RequiresExplicitApproval" INTEGER NOT NULL,
    "DefaultAllocationPercent" INTEGER NOT NULL,
    "RoleTemplateSourceKey" TEXT NOT NULL,
    "RoleTemplateSnapshotName" TEXT NOT NULL,
    "SnapshotSummary" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL
);

CREATE TABLE "Processes_RoleSkillRequirements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_RoleSkillRequirements" PRIMARY KEY,
    "RoleRequirementId" TEXT NOT NULL,
    "SkillId" TEXT NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    "MinimumYearsExperience" INTEGER NOT NULL
);

CREATE TABLE "Processes_RunAssignments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_RunAssignments" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "RoleRequirementId" TEXT NOT NULL,
    "StepDefinitionId" TEXT NULL,
    "PartyId" TEXT NULL,
    "DisplayName" TEXT NOT NULL,
    "ExecutorKind" TEXT NOT NULL,
    "BindingReason" TEXT NOT NULL,
    "SourceRegistryKey" TEXT NOT NULL,
    "SnapshotSummary" TEXT NOT NULL,
    "IsFallback" INTEGER NOT NULL,
    "IsCapabilityGap" INTEGER NOT NULL
);

CREATE TABLE "Processes_Runs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_Runs" PRIMARY KEY,
    "ProcessDefinitionId" TEXT NOT NULL,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "Name" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "OperatingMode" TEXT NOT NULL,
    "TriggerReason" TEXT NOT NULL,
    "GovernanceSnapshot" TEXT NOT NULL,
    "PolicySnapshot" TEXT NOT NULL,
    "ExecutorSnapshotSummary" TEXT NOT NULL,
    "ReplayPackageKey" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "StartedAtUtc" TEXT NULL,
    "CompletedAtUtc" TEXT NULL,
    "EstimatedCost" TEXT NOT NULL,
    "ActualCost" TEXT NOT NULL,
    "FirstTimeRightPercent" INTEGER NOT NULL,
    "SlaAttainmentPercent" INTEGER NOT NULL
);

CREATE TABLE "Processes_StepDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepDefinitions" PRIMARY KEY,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Subtitle" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "StepKind" TEXT NOT NULL,
    "AllowsManualSkip" INTEGER NOT NULL,
    "AllowsSafeRefusal" INTEGER NOT NULL,
    "RequiresApproval" INTEGER NOT NULL,
    "RequiresDecisionRecord" INTEGER NOT NULL,
    "InputContractSummary" TEXT NOT NULL,
    "OutputContractSummary" TEXT NOT NULL,
    "EvidenceContractSummary" TEXT NOT NULL,
    "DecisionRightsSummary" TEXT NOT NULL,
    "ExceptionPolicySummary" TEXT NOT NULL,
    "TargetLeadHours" INTEGER NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "DependsOnStepId" TEXT NULL,
    "CanvasX" REAL NOT NULL,
    "CanvasY" REAL NOT NULL
);

CREATE TABLE "Processes_StepRoleRequirements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepRoleRequirements" PRIMARY KEY,
    "StepDefinitionId" TEXT NOT NULL,
    "RoleRequirementId" TEXT NOT NULL,
    "ResponsibilityKind" TEXT NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    "FallbackOrder" INTEGER NOT NULL,
    "RebindPolicySummary" TEXT NOT NULL
);

CREATE TABLE "Processes_StepRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepRuns" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    "Sequence" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    "StepKind" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "RoleSnapshotSummary" TEXT NOT NULL,
    "CurrentExecutorName" TEXT NOT NULL,
    "CurrentExecutorPartyId" TEXT NULL,
    "DecisionSummary" TEXT NOT NULL,
    "BlockedReason" TEXT NOT NULL,
    "RefusalReason" TEXT NOT NULL,
    "ExceptionSummary" TEXT NOT NULL,
    "InputQualitySummary" TEXT NOT NULL,
    "ReadyAtUtc" TEXT NULL,
    "StartedAtUtc" TEXT NULL,
    "CompletedAtUtc" TEXT NULL,
    "WaitMinutes" INTEGER NOT NULL,
    "TouchMinutes" INTEGER NOT NULL,
    "BlockedMinutes" INTEGER NOT NULL,
    "ReworkCount" INTEGER NOT NULL,
    "CapabilityGapSeverity" TEXT NOT NULL
);

CREATE TABLE "Processes_WorkBriefs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_WorkBriefs" PRIMARY KEY,
    "ProcessRunId" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "WorkBriefText" TEXT NOT NULL,
    "HandoffSummary" TEXT NOT NULL,
    "AssignmentReason" TEXT NOT NULL,
    "ExpectedOutcome" TEXT NOT NULL,
    "EvidenceExpectationSummary" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
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

CREATE UNIQUE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNumber" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "VersionNumber");

CREATE INDEX "IX_Processes_ImprovementCandidates_ProcessDefinitionId" ON "Processes_ImprovementCandidates" ("ProcessDefinitionId");

CREATE INDEX "IX_Processes_ImprovementCandidates_ProcessRunId" ON "Processes_ImprovementCandidates" ("ProcessRunId");

CREATE INDEX "IX_Processes_ImprovementCandidates_Status" ON "Processes_ImprovementCandidates" ("Status");

CREATE INDEX "IX_Processes_JournalEntries_ProcessRunId_OccurredAtUtc" ON "Processes_JournalEntries" ("ProcessRunId", "OccurredAtUtc");

CREATE INDEX "IX_Processes_JournalEntries_StepRunId" ON "Processes_JournalEntries" ("StepRunId");

CREATE UNIQUE INDEX "IX_Processes_RoleRequirements_ProcessDefinitionVersionId_Key" ON "Processes_RoleRequirements" ("ProcessDefinitionVersionId", "Key");

CREATE UNIQUE INDEX "IX_Processes_RoleSkillRequirements_RoleRequirementId_SkillId" ON "Processes_RoleSkillRequirements" ("RoleRequirementId", "SkillId");

CREATE INDEX "IX_Processes_RoleSkillRequirements_SkillId" ON "Processes_RoleSkillRequirements" ("SkillId");

CREATE INDEX "IX_Processes_RunAssignments_PartyId" ON "Processes_RunAssignments" ("PartyId");

CREATE INDEX "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_StepDefinitionId" ON "Processes_RunAssignments" ("ProcessRunId", "RoleRequirementId", "StepDefinitionId");

CREATE INDEX "IX_Processes_Runs_ProcessDefinitionId" ON "Processes_Runs" ("ProcessDefinitionId");

CREATE INDEX "IX_Processes_Runs_ProjectId" ON "Processes_Runs" ("ProjectId");

CREATE INDEX "IX_Processes_Runs_Status" ON "Processes_Runs" ("Status");

CREATE INDEX "IX_Processes_StepDefinitions_DependsOnStepId" ON "Processes_StepDefinitions" ("DependsOnStepId");

CREATE UNIQUE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_Key" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "Key");

CREATE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_OrderIndex" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "OrderIndex");

CREATE UNIQUE INDEX "IX_Processes_StepRoleRequirements_StepDefinitionId_RoleRequirementId_ResponsibilityKind" ON "Processes_StepRoleRequirements" ("StepDefinitionId", "RoleRequirementId", "ResponsibilityKind");

CREATE UNIQUE INDEX "IX_Processes_StepRuns_ProcessRunId_Sequence" ON "Processes_StepRuns" ("ProcessRunId", "Sequence");

CREATE INDEX "IX_Processes_StepRuns_ProcessRunId_Status" ON "Processes_StepRuns" ("ProcessRunId", "Status");

CREATE INDEX "IX_Processes_StepRuns_StepDefinitionId" ON "Processes_StepRuns" ("StepDefinitionId");

CREATE INDEX "IX_Processes_WorkBriefs_ProcessRunId" ON "Processes_WorkBriefs" ("ProcessRunId");

CREATE INDEX "IX_Processes_WorkBriefs_StepRunId" ON "Processes_WorkBriefs" ("StepRunId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260409104531_AddProcessesFoundation', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Processes_StepRuns" ADD "SelectedBranchOutcomeId" TEXT NULL;

ALTER TABLE "Processes_StepRuns" ADD "SelectedBranchOutcomeTitle" TEXT NOT NULL DEFAULT '';

ALTER TABLE "Processes_StepDefinitions" ADD "DecisionRoleRequirementId" TEXT NULL;

ALTER TABLE "Processes_StepDefinitions" ADD "DependsOnBranchOutcomeId" TEXT NULL;

ALTER TABLE "Processes_DecisionRecords" ADD "BranchOutcomeId" TEXT NULL;

ALTER TABLE "Processes_DecisionRecords" ADD "BranchOutcomeTitle" TEXT NOT NULL DEFAULT '';

CREATE TABLE "Processes_StepBranchOutcomes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepBranchOutcomes" PRIMARY KEY,
    "StepDefinitionId" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL
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

BEGIN TRANSACTION;
ALTER TABLE "Processes_StepDefinitions" ADD "BranchCanvasX" REAL NOT NULL DEFAULT 0.0;

ALTER TABLE "Processes_StepDefinitions" ADD "BranchCanvasY" REAL NOT NULL DEFAULT 0.0;

ALTER TABLE "Processes_RoleRequirements" ADD "CanvasX" REAL NOT NULL DEFAULT 0.0;

ALTER TABLE "Processes_RoleRequirements" ADD "CanvasY" REAL NOT NULL DEFAULT 0.0;

CREATE TABLE "Processes_StepDependencies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepDependencies" PRIMARY KEY,
    "StepDefinitionId" TEXT NOT NULL,
    "DependsOnStepId" TEXT NOT NULL,
    "DependsOnBranchOutcomeId" TEXT NULL,
    "DisplayOrder" INTEGER NOT NULL
);

CREATE INDEX "IX_Processes_StepDependencies_DependsOnBranchOutcomeId" ON "Processes_StepDependencies" ("DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDependencies_DependsOnStepId" ON "Processes_StepDependencies" ("DependsOnStepId");

CREATE INDEX "IX_Processes_StepDependencies_StepDefinitionId" ON "Processes_StepDependencies" ("StepDefinitionId");

CREATE UNIQUE INDEX "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId_DependsOnBranchOutcomeId" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDependencies_StepDefinitionId_DisplayOrder" ON "Processes_StepDependencies" ("StepDefinitionId", "DisplayOrder");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260411035614_AddProcessCanvasPositionsAndStepDependencies', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "Processes_StepArtifactInputs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepArtifactInputs" PRIMARY KEY,
    "StepDefinitionId" TEXT NOT NULL,
    "ArtifactExpectationId" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL
);

CREATE INDEX "IX_Processes_StepArtifactInputs_ArtifactExpectationId" ON "Processes_StepArtifactInputs" ("ArtifactExpectationId");

CREATE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId" ON "Processes_StepArtifactInputs" ("StepDefinitionId");

CREATE UNIQUE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId_ArtifactExpectationId" ON "Processes_StepArtifactInputs" ("StepDefinitionId", "ArtifactExpectationId");

CREATE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId_DisplayOrder" ON "Processes_StepArtifactInputs" ("StepDefinitionId", "DisplayOrder");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260411121254_AddProcessArtifactInputs', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Processes_StepRuns" ADD "ConcurrencyToken" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Processes_Runs" ADD "ConcurrencyToken" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Processes_DefinitionVersions" ADD "ConcurrencyToken" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Processes_Definitions" ADD "ConcurrencyToken" TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413101258_AddProcessOptimisticConcurrencyTokens', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "ef_temp_Processes_DefinitionVersions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_DefinitionVersions" PRIMARY KEY,
    "ChangeSummary" TEXT NOT NULL,
    "ConcurrencyToken" TEXT NOT NULL,
    "ConstitutionRuleSummary" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "GovernancePolicySummary" TEXT NOT NULL,
    "ImportWarnings" TEXT NOT NULL,
    "ImportedFrom" TEXT NOT NULL,
    "OperatingModeSummary" TEXT NOT NULL,
    "ProcessDefinitionId" TEXT NOT NULL,
    "PublishedAtUtc" TEXT NULL,
    "PublishedBy" TEXT NOT NULL,
    "SimulationReadinessSummary" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    CONSTRAINT "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessDefinitionId" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_DefinitionVersions" ("Id", "ChangeSummary", "ConcurrencyToken", "ConstitutionRuleSummary", "CreatedAtUtc", "GovernancePolicySummary", "ImportWarnings", "ImportedFrom", "OperatingModeSummary", "ProcessDefinitionId", "PublishedAtUtc", "PublishedBy", "SimulationReadinessSummary", "Status", "UpdatedAtUtc", "VersionNumber")
SELECT "Id", "ChangeSummary", "ConcurrencyToken", "ConstitutionRuleSummary", "CreatedAtUtc", "GovernancePolicySummary", "ImportWarnings", "ImportedFrom", "OperatingModeSummary", "ProcessDefinitionId", "PublishedAtUtc", "PublishedBy", "SimulationReadinessSummary", "Status", "UpdatedAtUtc", "VersionNumber"
FROM "Processes_DefinitionVersions";

CREATE TABLE "ef_temp_Processes_RoleRequirements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_RoleRequirements" PRIMARY KEY,
    "AllowsFallback" INTEGER NOT NULL,
    "CanvasX" REAL NOT NULL,
    "CanvasY" REAL NOT NULL,
    "DefaultAllocationPercent" INTEGER NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    "Key" TEXT NOT NULL,
    "PreferredExecutorKind" TEXT NOT NULL,
    "PreferredProjectAssignmentRole" TEXT NULL,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "Purpose" TEXT NOT NULL,
    "RequiresExplicitApproval" INTEGER NOT NULL,
    "RoleTemplateSnapshotName" TEXT NOT NULL,
    "RoleTemplateSourceKey" TEXT NOT NULL,
    "SnapshotSummary" TEXT NOT NULL,
    "StaffingIntent" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_RoleRequirements_Processes_DefinitionVersions_ProcessDefinitionVersionId" FOREIGN KEY ("ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_RoleRequirements" ("Id", "AllowsFallback", "CanvasX", "CanvasY", "DefaultAllocationPercent", "DisplayName", "DisplayOrder", "IsRequired", "Key", "PreferredExecutorKind", "PreferredProjectAssignmentRole", "ProcessDefinitionVersionId", "Purpose", "RequiresExplicitApproval", "RoleTemplateSnapshotName", "RoleTemplateSourceKey", "SnapshotSummary", "StaffingIntent")
SELECT "Id", "AllowsFallback", "CanvasX", "CanvasY", "DefaultAllocationPercent", "DisplayName", "DisplayOrder", "IsRequired", "Key", "PreferredExecutorKind", "PreferredProjectAssignmentRole", "ProcessDefinitionVersionId", "Purpose", "RequiresExplicitApproval", "RoleTemplateSnapshotName", "RoleTemplateSourceKey", "SnapshotSummary", "StaffingIntent"
FROM "Processes_RoleRequirements";

CREATE TABLE "ef_temp_Processes_RoleSkillRequirements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_RoleSkillRequirements" PRIMARY KEY,
    "IsRequired" INTEGER NOT NULL,
    "MinimumYearsExperience" INTEGER NOT NULL,
    "RoleRequirementId" TEXT NOT NULL,
    "SkillId" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_RoleSkillRequirements_Processes_RoleRequirements_RoleRequirementId" FOREIGN KEY ("RoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_RoleSkillRequirements" ("Id", "IsRequired", "MinimumYearsExperience", "RoleRequirementId", "SkillId")
SELECT "Id", "IsRequired", "MinimumYearsExperience", "RoleRequirementId", "SkillId"
FROM "Processes_RoleSkillRequirements";

CREATE TABLE "ef_temp_Processes_StepDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepDefinitions" PRIMARY KEY,
    "AllowsManualSkip" INTEGER NOT NULL,
    "AllowsSafeRefusal" INTEGER NOT NULL,
    "BranchCanvasX" REAL NOT NULL,
    "BranchCanvasY" REAL NOT NULL,
    "CanvasX" REAL NOT NULL,
    "CanvasY" REAL NOT NULL,
    "DecisionRightsSummary" TEXT NOT NULL,
    "DecisionRoleRequirementId" TEXT NULL,
    "DependsOnBranchOutcomeId" TEXT NULL,
    "DependsOnStepId" TEXT NULL,
    "EvidenceContractSummary" TEXT NOT NULL,
    "ExceptionPolicySummary" TEXT NOT NULL,
    "InputContractSummary" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "OutputContractSummary" TEXT NOT NULL,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "RequiresApproval" INTEGER NOT NULL,
    "RequiresDecisionRecord" INTEGER NOT NULL,
    "StepKind" TEXT NOT NULL,
    "Subtitle" TEXT NOT NULL,
    "TargetLeadHours" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_StepDefinitions_Processes_DefinitionVersions_ProcessDefinitionVersionId" FOREIGN KEY ("ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_StepDefinitions_Processes_RoleRequirements_DecisionRoleRequirementId" FOREIGN KEY ("DecisionRoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Processes_StepDefinitions" ("Id", "AllowsManualSkip", "AllowsSafeRefusal", "BranchCanvasX", "BranchCanvasY", "CanvasX", "CanvasY", "DecisionRightsSummary", "DecisionRoleRequirementId", "DependsOnBranchOutcomeId", "DependsOnStepId", "EvidenceContractSummary", "ExceptionPolicySummary", "InputContractSummary", "Key", "Notes", "OrderIndex", "OutputContractSummary", "ProcessDefinitionVersionId", "RequiresApproval", "RequiresDecisionRecord", "StepKind", "Subtitle", "TargetLeadHours", "Title")
SELECT "Id", "AllowsManualSkip", "AllowsSafeRefusal", "BranchCanvasX", "BranchCanvasY", "CanvasX", "CanvasY", "DecisionRightsSummary", "DecisionRoleRequirementId", "DependsOnBranchOutcomeId", "DependsOnStepId", "EvidenceContractSummary", "ExceptionPolicySummary", "InputContractSummary", "Key", "Notes", "OrderIndex", "OutputContractSummary", "ProcessDefinitionVersionId", "RequiresApproval", "RequiresDecisionRecord", "StepKind", "Subtitle", "TargetLeadHours", "Title"
FROM "Processes_StepDefinitions";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Processes_DefinitionVersions";

ALTER TABLE "ef_temp_Processes_DefinitionVersions" RENAME TO "Processes_DefinitionVersions";

DROP TABLE "Processes_RoleRequirements";

ALTER TABLE "ef_temp_Processes_RoleRequirements" RENAME TO "Processes_RoleRequirements";

DROP TABLE "Processes_RoleSkillRequirements";

ALTER TABLE "ef_temp_Processes_RoleSkillRequirements" RENAME TO "Processes_RoleSkillRequirements";

DROP TABLE "Processes_StepDefinitions";

ALTER TABLE "ef_temp_Processes_StepDefinitions" RENAME TO "Processes_StepDefinitions";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "Status");

CREATE UNIQUE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNumber" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "VersionNumber");

CREATE UNIQUE INDEX "IX_Processes_RoleRequirements_ProcessDefinitionVersionId_Key" ON "Processes_RoleRequirements" ("ProcessDefinitionVersionId", "Key");

CREATE UNIQUE INDEX "IX_Processes_RoleSkillRequirements_RoleRequirementId_SkillId" ON "Processes_RoleSkillRequirements" ("RoleRequirementId", "SkillId");

CREATE INDEX "IX_Processes_RoleSkillRequirements_SkillId" ON "Processes_RoleSkillRequirements" ("SkillId");

CREATE INDEX "IX_Processes_StepDefinitions_DecisionRoleRequirementId" ON "Processes_StepDefinitions" ("DecisionRoleRequirementId");

CREATE INDEX "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId" ON "Processes_StepDefinitions" ("DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDefinitions_DependsOnStepId" ON "Processes_StepDefinitions" ("DependsOnStepId");

CREATE UNIQUE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_Key" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "Key");

CREATE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_OrderIndex" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "OrderIndex");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413144750_AddProcessDefinitionForeignKeys', '10.0.4');

BEGIN TRANSACTION;
DROP INDEX "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId_DependsOnBranchOutcomeId";

DROP INDEX "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId";

DROP INDEX "IX_Processes_StepDefinitions_DependsOnStepId";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Processes_DefinitionVersions_ProcessDefinitionId_Id_MigrationSupport"
ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "Id");

CREATE INDEX "IX_Processes_StepRoleRequirements_RoleRequirementId" ON "Processes_StepRoleRequirements" ("RoleRequirementId");

CREATE UNIQUE INDEX "UX_ProcessStepDeps_Conditional" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId") WHERE "DependsOnBranchOutcomeId" IS NOT NULL;

CREATE UNIQUE INDEX "UX_ProcessStepDeps_Unconditional" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId") WHERE "DependsOnBranchOutcomeId" IS NULL;

CREATE INDEX "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersionId" ON "Processes_Runs" ("ProcessDefinitionId", "ProcessDefinitionVersionId");

CREATE INDEX "IX_Processes_RunAssignments_RoleRequirementId" ON "Processes_RunAssignments" ("RoleRequirementId");

CREATE INDEX "IX_Processes_RunAssignments_StepDefinitionId" ON "Processes_RunAssignments" ("StepDefinitionId");

CREATE TABLE "ef_temp_Processes_StepDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepDefinitions" PRIMARY KEY,
    "AllowsManualSkip" INTEGER NOT NULL,
    "AllowsSafeRefusal" INTEGER NOT NULL,
    "BranchCanvasX" REAL NOT NULL,
    "BranchCanvasY" REAL NOT NULL,
    "CanvasX" REAL NOT NULL,
    "CanvasY" REAL NOT NULL,
    "DecisionRightsSummary" TEXT NOT NULL,
    "DecisionRoleRequirementId" TEXT NULL,
    "EvidenceContractSummary" TEXT NOT NULL,
    "ExceptionPolicySummary" TEXT NOT NULL,
    "InputContractSummary" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Notes" TEXT NOT NULL,
    "OrderIndex" INTEGER NOT NULL,
    "OutputContractSummary" TEXT NOT NULL,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "RequiresApproval" INTEGER NOT NULL,
    "RequiresDecisionRecord" INTEGER NOT NULL,
    "StepKind" TEXT NOT NULL,
    "Subtitle" TEXT NOT NULL,
    "TargetLeadHours" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_StepDefinitions_Processes_DefinitionVersions_ProcessDefinitionVersionId" FOREIGN KEY ("ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_StepDefinitions_Processes_RoleRequirements_DecisionRoleRequirementId" FOREIGN KEY ("DecisionRoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Processes_StepDefinitions" ("Id", "AllowsManualSkip", "AllowsSafeRefusal", "BranchCanvasX", "BranchCanvasY", "CanvasX", "CanvasY", "DecisionRightsSummary", "DecisionRoleRequirementId", "EvidenceContractSummary", "ExceptionPolicySummary", "InputContractSummary", "Key", "Notes", "OrderIndex", "OutputContractSummary", "ProcessDefinitionVersionId", "RequiresApproval", "RequiresDecisionRecord", "StepKind", "Subtitle", "TargetLeadHours", "Title")
SELECT "Id", "AllowsManualSkip", "AllowsSafeRefusal", "BranchCanvasX", "BranchCanvasY", "CanvasX", "CanvasY", "DecisionRightsSummary", "DecisionRoleRequirementId", "EvidenceContractSummary", "ExceptionPolicySummary", "InputContractSummary", "Key", "Notes", "OrderIndex", "OutputContractSummary", "ProcessDefinitionVersionId", "RequiresApproval", "RequiresDecisionRecord", "StepKind", "Subtitle", "TargetLeadHours", "Title"
FROM "Processes_StepDefinitions";

CREATE TABLE "ef_temp_Processes_DefinitionVersions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_DefinitionVersions" PRIMARY KEY,
    "ChangeSummary" TEXT NOT NULL,
    "ConcurrencyToken" TEXT NOT NULL,
    "ConstitutionRuleSummary" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "GovernancePolicySummary" TEXT NOT NULL,
    "ImportWarnings" TEXT NOT NULL,
    "ImportedFrom" TEXT NOT NULL,
    "OperatingModeSummary" TEXT NOT NULL,
    "ProcessDefinitionId" TEXT NOT NULL,
    "PublishedAtUtc" TEXT NULL,
    "PublishedBy" TEXT NOT NULL,
    "SimulationReadinessSummary" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "VersionNumber" INTEGER NOT NULL,
    CONSTRAINT "AK_Processes_DefinitionVersions_ProcessDefinitionId_Id" UNIQUE ("ProcessDefinitionId", "Id"),
    CONSTRAINT "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessDefinitionId" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_DefinitionVersions" ("Id", "ChangeSummary", "ConcurrencyToken", "ConstitutionRuleSummary", "CreatedAtUtc", "GovernancePolicySummary", "ImportWarnings", "ImportedFrom", "OperatingModeSummary", "ProcessDefinitionId", "PublishedAtUtc", "PublishedBy", "SimulationReadinessSummary", "Status", "UpdatedAtUtc", "VersionNumber")
SELECT "Id", "ChangeSummary", "ConcurrencyToken", "ConstitutionRuleSummary", "CreatedAtUtc", "GovernancePolicySummary", "ImportWarnings", "ImportedFrom", "OperatingModeSummary", "ProcessDefinitionId", "PublishedAtUtc", "PublishedBy", "SimulationReadinessSummary", "Status", "UpdatedAtUtc", "VersionNumber"
FROM "Processes_DefinitionVersions";

CREATE TABLE "ef_temp_Processes_ArtifactExpectations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ArtifactExpectations" PRIMARY KEY,
    "AllowedFutureUsageSummary" TEXT NOT NULL,
    "ArtifactKind" TEXT NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    "RetentionDays" INTEGER NOT NULL,
    "SensitivityLevel" TEXT NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "TrustRequirement" TEXT NOT NULL,
    "ValidationRequirementSummary" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_ArtifactExpectations_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_ArtifactExpectations" ("Id", "AllowedFutureUsageSummary", "ArtifactKind", "IsRequired", "RetentionDays", "SensitivityLevel", "StepDefinitionId", "Title", "TrustRequirement", "ValidationRequirementSummary")
SELECT "Id", "AllowedFutureUsageSummary", "ArtifactKind", "IsRequired", "RetentionDays", "SensitivityLevel", "StepDefinitionId", "Title", "TrustRequirement", "ValidationRequirementSummary"
FROM "Processes_ArtifactExpectations";

CREATE TABLE "ef_temp_Processes_ArtifactRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ArtifactRecords" PRIMARY KEY,
    "AllowedFutureUsageSummary" TEXT NOT NULL,
    "ArtifactKind" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "ExternalReferenceKey" TEXT NOT NULL,
    "ManagedStoragePath" TEXT NOT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "ProvenanceSummary" TEXT NOT NULL,
    "ReviewSummary" TEXT NOT NULL,
    "SensitivityLevel" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "TrustStatus" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_ArtifactRecords_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_ArtifactRecords_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_ArtifactRecords" ("Id", "AllowedFutureUsageSummary", "ArtifactKind", "CreatedAtUtc", "ExternalReferenceKey", "ManagedStoragePath", "ProcessRunId", "ProvenanceSummary", "ReviewSummary", "SensitivityLevel", "StepRunId", "Title", "TrustStatus")
SELECT "Id", "AllowedFutureUsageSummary", "ArtifactKind", "CreatedAtUtc", "ExternalReferenceKey", "ManagedStoragePath", "ProcessRunId", "ProvenanceSummary", "ReviewSummary", "SensitivityLevel", "StepRunId", "Title", "TrustStatus"
FROM "Processes_ArtifactRecords";

CREATE TABLE "ef_temp_Processes_ConformanceObservations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ConformanceObservations" PRIMARY KEY,
    "Category" TEXT NOT NULL,
    "ContainsSensitiveAssessment" INTEGER NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "DeviationReason" TEXT NOT NULL,
    "IsSafeNonAction" INTEGER NOT NULL,
    "Observation" TEXT NOT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "Severity" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    CONSTRAINT "FK_Processes_ConformanceObservations_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_ConformanceObservations_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_ConformanceObservations" ("Id", "Category", "ContainsSensitiveAssessment", "CreatedAtUtc", "DeviationReason", "IsSafeNonAction", "Observation", "ProcessRunId", "Severity", "StepRunId")
SELECT "Id", "Category", "ContainsSensitiveAssessment", "CreatedAtUtc", "DeviationReason", "IsSafeNonAction", "Observation", "ProcessRunId", "Severity", "StepRunId"
FROM "Processes_ConformanceObservations";

CREATE TABLE "ef_temp_Processes_DecisionRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_DecisionRecords" PRIMARY KEY,
    "BranchOutcomeId" TEXT NULL,
    "BranchOutcomeTitle" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "DecidedBy" TEXT NOT NULL,
    "DecisionKind" TEXT NOT NULL,
    "OperatingMode" TEXT NOT NULL,
    "Outcome" TEXT NOT NULL,
    "PolicyEvaluation" TEXT NOT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_DecisionRecords_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_DecisionRecords_Processes_StepBranchOutcomes_BranchOutcomeId" FOREIGN KEY ("BranchOutcomeId") REFERENCES "Processes_StepBranchOutcomes" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Processes_DecisionRecords_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_DecisionRecords" ("Id", "BranchOutcomeId", "BranchOutcomeTitle", "CreatedAtUtc", "DecidedBy", "DecisionKind", "OperatingMode", "Outcome", "PolicyEvaluation", "ProcessRunId", "Reason", "StepRunId", "Title")
SELECT "Id", "BranchOutcomeId", "BranchOutcomeTitle", "CreatedAtUtc", "DecidedBy", "DecisionKind", "OperatingMode", "Outcome", "PolicyEvaluation", "ProcessRunId", "Reason", "StepRunId", "Title"
FROM "Processes_DecisionRecords";

CREATE TABLE "ef_temp_Processes_ImprovementCandidates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_ImprovementCandidates" PRIMARY KEY,
    "Category" TEXT NOT NULL,
    "ClosedAtUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "EvidenceSummary" TEXT NOT NULL,
    "IsTrainingOpportunity" INTEGER NOT NULL,
    "ProblemSummary" TEXT NOT NULL,
    "ProcessDefinitionId" TEXT NOT NULL,
    "ProcessRunId" TEXT NULL,
    "RequiresGovernanceReview" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_ImprovementCandidates_Processes_Definitions_ProcessDefinitionId" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_ImprovementCandidates_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_ImprovementCandidates" ("Id", "Category", "ClosedAtUtc", "CreatedAtUtc", "EvidenceSummary", "IsTrainingOpportunity", "ProblemSummary", "ProcessDefinitionId", "ProcessRunId", "RequiresGovernanceReview", "Status", "Title")
SELECT "Id", "Category", "ClosedAtUtc", "CreatedAtUtc", "EvidenceSummary", "IsTrainingOpportunity", "ProblemSummary", "ProcessDefinitionId", "ProcessRunId", "RequiresGovernanceReview", "Status", "Title"
FROM "Processes_ImprovementCandidates";

CREATE TABLE "ef_temp_Processes_JournalEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_JournalEntries" PRIMARY KEY,
    "CorrelationId" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "EnvironmentMode" TEXT NOT NULL,
    "EventType" TEXT NOT NULL,
    "OccurredAtUtc" TEXT NOT NULL,
    "OperatingMode" TEXT NOT NULL,
    "PolicyVersion" TEXT NOT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "ReplayContextJson" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_JournalEntries_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_JournalEntries_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_JournalEntries" ("Id", "CorrelationId", "Description", "EnvironmentMode", "EventType", "OccurredAtUtc", "OperatingMode", "PolicyVersion", "ProcessRunId", "ReplayContextJson", "StepRunId", "Title")
SELECT "Id", "CorrelationId", "Description", "EnvironmentMode", "EventType", "OccurredAtUtc", "OperatingMode", "PolicyVersion", "ProcessRunId", "ReplayContextJson", "StepRunId", "Title"
FROM "Processes_JournalEntries";

CREATE TABLE "ef_temp_Processes_RunAssignments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_RunAssignments" PRIMARY KEY,
    "BindingReason" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "ExecutorKind" TEXT NOT NULL,
    "IsCapabilityGap" INTEGER NOT NULL,
    "IsFallback" INTEGER NOT NULL,
    "PartyId" TEXT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "RoleRequirementId" TEXT NOT NULL,
    "SnapshotSummary" TEXT NOT NULL,
    "SourceRegistryKey" TEXT NOT NULL,
    "StepDefinitionId" TEXT NULL,
    CONSTRAINT "FK_Processes_RunAssignments_Processes_RoleRequirements_RoleRequirementId" FOREIGN KEY ("RoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Processes_RunAssignments_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_RunAssignments_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_RunAssignments" ("Id", "BindingReason", "DisplayName", "ExecutorKind", "IsCapabilityGap", "IsFallback", "PartyId", "ProcessRunId", "RoleRequirementId", "SnapshotSummary", "SourceRegistryKey", "StepDefinitionId")
SELECT "Id", "BindingReason", "DisplayName", "ExecutorKind", "IsCapabilityGap", "IsFallback", "PartyId", "ProcessRunId", "RoleRequirementId", "SnapshotSummary", "SourceRegistryKey", "StepDefinitionId"
FROM "Processes_RunAssignments";

CREATE TABLE "ef_temp_Processes_Runs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_Runs" PRIMARY KEY,
    "ActualCost" TEXT NOT NULL,
    "CompletedAtUtc" TEXT NULL,
    "ConcurrencyToken" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "EstimatedCost" TEXT NOT NULL,
    "ExecutorSnapshotSummary" TEXT NOT NULL,
    "FirstTimeRightPercent" INTEGER NOT NULL,
    "GovernanceSnapshot" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "OperatingMode" TEXT NOT NULL,
    "PolicySnapshot" TEXT NOT NULL,
    "ProcessDefinitionId" TEXT NOT NULL,
    "ProcessDefinitionVersionId" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "ReplayPackageKey" TEXT NOT NULL,
    "SlaAttainmentPercent" INTEGER NOT NULL,
    "StartedAtUtc" TEXT NULL,
    "Status" TEXT NOT NULL,
    "TriggerReason" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_Runs_Processes_DefinitionVersions_ProcessDefinitionId_ProcessDefinitionVersionId" FOREIGN KEY ("ProcessDefinitionId", "ProcessDefinitionVersionId") REFERENCES "Processes_DefinitionVersions" ("ProcessDefinitionId", "Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Processes_Runs_Processes_Definitions_ProcessDefinitionId" FOREIGN KEY ("ProcessDefinitionId") REFERENCES "Processes_Definitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_Runs" ("Id", "ActualCost", "CompletedAtUtc", "ConcurrencyToken", "CreatedAtUtc", "EstimatedCost", "ExecutorSnapshotSummary", "FirstTimeRightPercent", "GovernanceSnapshot", "Name", "OperatingMode", "PolicySnapshot", "ProcessDefinitionId", "ProcessDefinitionVersionId", "ProjectId", "ReplayPackageKey", "SlaAttainmentPercent", "StartedAtUtc", "Status", "TriggerReason", "UpdatedAtUtc")
SELECT "Id", "ActualCost", "CompletedAtUtc", "ConcurrencyToken", "CreatedAtUtc", "EstimatedCost", "ExecutorSnapshotSummary", "FirstTimeRightPercent", "GovernanceSnapshot", "Name", "OperatingMode", "PolicySnapshot", "ProcessDefinitionId", "ProcessDefinitionVersionId", "ProjectId", "ReplayPackageKey", "SlaAttainmentPercent", "StartedAtUtc", "Status", "TriggerReason", "UpdatedAtUtc"
FROM "Processes_Runs";

CREATE TABLE "ef_temp_Processes_StepArtifactInputs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepArtifactInputs" PRIMARY KEY,
    "ArtifactExpectationId" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_StepArtifactInputs_Processes_ArtifactExpectations_ArtifactExpectationId" FOREIGN KEY ("ArtifactExpectationId") REFERENCES "Processes_ArtifactExpectations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Processes_StepArtifactInputs_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_StepArtifactInputs" ("Id", "ArtifactExpectationId", "DisplayOrder", "StepDefinitionId")
SELECT "Id", "ArtifactExpectationId", "DisplayOrder", "StepDefinitionId"
FROM "Processes_StepArtifactInputs";

CREATE TABLE "ef_temp_Processes_StepBranchOutcomes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepBranchOutcomes" PRIMARY KEY,
    "Description" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "Key" TEXT NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_StepBranchOutcomes" ("Id", "Description", "DisplayOrder", "Key", "StepDefinitionId", "Title")
SELECT "Id", "Description", "DisplayOrder", "Key", "StepDefinitionId", "Title"
FROM "Processes_StepBranchOutcomes";

CREATE TABLE "ef_temp_Processes_StepDependencies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepDependencies" PRIMARY KEY,
    "DependsOnBranchOutcomeId" TEXT NULL,
    "DependsOnStepId" TEXT NOT NULL,
    "DisplayOrder" INTEGER NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_StepDependencies_Processes_StepBranchOutcomes_DependsOnBranchOutcomeId" FOREIGN KEY ("DependsOnBranchOutcomeId") REFERENCES "Processes_StepBranchOutcomes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Processes_StepDependencies_Processes_StepDefinitions_DependsOnStepId" FOREIGN KEY ("DependsOnStepId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Processes_StepDependencies_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_StepDependencies" ("Id", "DependsOnBranchOutcomeId", "DependsOnStepId", "DisplayOrder", "StepDefinitionId")
SELECT "Id", "DependsOnBranchOutcomeId", "DependsOnStepId", "DisplayOrder", "StepDefinitionId"
FROM "Processes_StepDependencies";

CREATE TABLE "ef_temp_Processes_StepRoleRequirements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepRoleRequirements" PRIMARY KEY,
    "FallbackOrder" INTEGER NOT NULL,
    "IsRequired" INTEGER NOT NULL,
    "RebindPolicySummary" TEXT NOT NULL,
    "ResponsibilityKind" TEXT NOT NULL,
    "RoleRequirementId" TEXT NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_StepRoleRequirements_Processes_RoleRequirements_RoleRequirementId" FOREIGN KEY ("RoleRequirementId") REFERENCES "Processes_RoleRequirements" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Processes_StepRoleRequirements_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE CASCADE
);

INSERT INTO "ef_temp_Processes_StepRoleRequirements" ("Id", "FallbackOrder", "IsRequired", "RebindPolicySummary", "ResponsibilityKind", "RoleRequirementId", "StepDefinitionId")
SELECT "Id", "FallbackOrder", "IsRequired", "RebindPolicySummary", "ResponsibilityKind", "RoleRequirementId", "StepDefinitionId"
FROM "Processes_StepRoleRequirements";

CREATE TABLE "ef_temp_Processes_StepRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_StepRuns" PRIMARY KEY,
    "BlockedMinutes" INTEGER NOT NULL,
    "BlockedReason" TEXT NOT NULL,
    "CapabilityGapSeverity" TEXT NOT NULL,
    "CompletedAtUtc" TEXT NULL,
    "ConcurrencyToken" TEXT NOT NULL,
    "CurrentExecutorName" TEXT NOT NULL,
    "CurrentExecutorPartyId" TEXT NULL,
    "DecisionSummary" TEXT NOT NULL,
    "ExceptionSummary" TEXT NOT NULL,
    "InputQualitySummary" TEXT NOT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "ReadyAtUtc" TEXT NULL,
    "RefusalReason" TEXT NOT NULL,
    "ReworkCount" INTEGER NOT NULL,
    "RoleSnapshotSummary" TEXT NOT NULL,
    "SelectedBranchOutcomeId" TEXT NULL,
    "SelectedBranchOutcomeTitle" TEXT NOT NULL,
    "Sequence" INTEGER NOT NULL,
    "StartedAtUtc" TEXT NULL,
    "Status" TEXT NOT NULL,
    "StepDefinitionId" TEXT NOT NULL,
    "StepKind" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "TouchMinutes" INTEGER NOT NULL,
    "WaitMinutes" INTEGER NOT NULL,
    CONSTRAINT "FK_Processes_StepRuns_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_StepRuns_Processes_StepBranchOutcomes_SelectedBranchOutcomeId" FOREIGN KEY ("SelectedBranchOutcomeId") REFERENCES "Processes_StepBranchOutcomes" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinitionId" FOREIGN KEY ("StepDefinitionId") REFERENCES "Processes_StepDefinitions" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Processes_StepRuns" ("Id", "BlockedMinutes", "BlockedReason", "CapabilityGapSeverity", "CompletedAtUtc", "ConcurrencyToken", "CurrentExecutorName", "CurrentExecutorPartyId", "DecisionSummary", "ExceptionSummary", "InputQualitySummary", "ProcessRunId", "ReadyAtUtc", "RefusalReason", "ReworkCount", "RoleSnapshotSummary", "SelectedBranchOutcomeId", "SelectedBranchOutcomeTitle", "Sequence", "StartedAtUtc", "Status", "StepDefinitionId", "StepKind", "Title", "TouchMinutes", "WaitMinutes")
SELECT "Id", "BlockedMinutes", "BlockedReason", "CapabilityGapSeverity", "CompletedAtUtc", "ConcurrencyToken", "CurrentExecutorName", "CurrentExecutorPartyId", "DecisionSummary", "ExceptionSummary", "InputQualitySummary", "ProcessRunId", "ReadyAtUtc", "RefusalReason", "ReworkCount", "RoleSnapshotSummary", "SelectedBranchOutcomeId", "SelectedBranchOutcomeTitle", "Sequence", "StartedAtUtc", "Status", "StepDefinitionId", "StepKind", "Title", "TouchMinutes", "WaitMinutes"
FROM "Processes_StepRuns";

CREATE TABLE "ef_temp_Processes_WorkBriefs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_WorkBriefs" PRIMARY KEY,
    "AssignmentReason" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "EvidenceExpectationSummary" TEXT NOT NULL,
    "ExpectedOutcome" TEXT NOT NULL,
    "HandoffSummary" TEXT NOT NULL,
    "ProcessRunId" TEXT NOT NULL,
    "StepRunId" TEXT NULL,
    "Title" TEXT NOT NULL,
    "WorkBriefText" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_WorkBriefs_Processes_Runs_ProcessRunId" FOREIGN KEY ("ProcessRunId") REFERENCES "Processes_Runs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Processes_WorkBriefs_Processes_StepRuns_StepRunId" FOREIGN KEY ("StepRunId") REFERENCES "Processes_StepRuns" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Processes_WorkBriefs" ("Id", "AssignmentReason", "CreatedAtUtc", "EvidenceExpectationSummary", "ExpectedOutcome", "HandoffSummary", "ProcessRunId", "StepRunId", "Title", "WorkBriefText")
SELECT "Id", "AssignmentReason", "CreatedAtUtc", "EvidenceExpectationSummary", "ExpectedOutcome", "HandoffSummary", "ProcessRunId", "StepRunId", "Title", "WorkBriefText"
FROM "Processes_WorkBriefs";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Processes_StepDefinitions";

ALTER TABLE "ef_temp_Processes_StepDefinitions" RENAME TO "Processes_StepDefinitions";

DROP TABLE "Processes_DefinitionVersions";

ALTER TABLE "ef_temp_Processes_DefinitionVersions" RENAME TO "Processes_DefinitionVersions";

DROP TABLE "Processes_ArtifactExpectations";

ALTER TABLE "ef_temp_Processes_ArtifactExpectations" RENAME TO "Processes_ArtifactExpectations";

DROP TABLE "Processes_ArtifactRecords";

ALTER TABLE "ef_temp_Processes_ArtifactRecords" RENAME TO "Processes_ArtifactRecords";

DROP TABLE "Processes_ConformanceObservations";

ALTER TABLE "ef_temp_Processes_ConformanceObservations" RENAME TO "Processes_ConformanceObservations";

DROP TABLE "Processes_DecisionRecords";

ALTER TABLE "ef_temp_Processes_DecisionRecords" RENAME TO "Processes_DecisionRecords";

DROP TABLE "Processes_ImprovementCandidates";

ALTER TABLE "ef_temp_Processes_ImprovementCandidates" RENAME TO "Processes_ImprovementCandidates";

DROP TABLE "Processes_JournalEntries";

ALTER TABLE "ef_temp_Processes_JournalEntries" RENAME TO "Processes_JournalEntries";

DROP TABLE "Processes_RunAssignments";

ALTER TABLE "ef_temp_Processes_RunAssignments" RENAME TO "Processes_RunAssignments";

DROP TABLE "Processes_Runs";

ALTER TABLE "ef_temp_Processes_Runs" RENAME TO "Processes_Runs";

DROP TABLE "Processes_StepArtifactInputs";

ALTER TABLE "ef_temp_Processes_StepArtifactInputs" RENAME TO "Processes_StepArtifactInputs";

DROP TABLE "Processes_StepBranchOutcomes";

ALTER TABLE "ef_temp_Processes_StepBranchOutcomes" RENAME TO "Processes_StepBranchOutcomes";

DROP TABLE "Processes_StepDependencies";

ALTER TABLE "ef_temp_Processes_StepDependencies" RENAME TO "Processes_StepDependencies";

DROP TABLE "Processes_StepRoleRequirements";

ALTER TABLE "ef_temp_Processes_StepRoleRequirements" RENAME TO "Processes_StepRoleRequirements";

DROP TABLE "Processes_StepRuns";

ALTER TABLE "ef_temp_Processes_StepRuns" RENAME TO "Processes_StepRuns";

DROP TABLE "Processes_WorkBriefs";

ALTER TABLE "ef_temp_Processes_WorkBriefs" RENAME TO "Processes_WorkBriefs";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Processes_StepDefinitions_DecisionRoleRequirementId" ON "Processes_StepDefinitions" ("DecisionRoleRequirementId");

CREATE UNIQUE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_Key" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "Key");

CREATE INDEX "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_OrderIndex" ON "Processes_StepDefinitions" ("ProcessDefinitionVersionId", "OrderIndex");

CREATE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "Status");

CREATE UNIQUE INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNumber" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "VersionNumber");

CREATE INDEX "IX_Processes_ArtifactExpectations_StepDefinitionId" ON "Processes_ArtifactExpectations" ("StepDefinitionId");

CREATE INDEX "IX_Processes_ArtifactRecords_ProcessRunId" ON "Processes_ArtifactRecords" ("ProcessRunId");

CREATE INDEX "IX_Processes_ArtifactRecords_StepRunId" ON "Processes_ArtifactRecords" ("StepRunId");

CREATE INDEX "IX_Processes_ConformanceObservations_ProcessRunId" ON "Processes_ConformanceObservations" ("ProcessRunId");

CREATE INDEX "IX_Processes_ConformanceObservations_StepRunId" ON "Processes_ConformanceObservations" ("StepRunId");

CREATE INDEX "IX_Processes_DecisionRecords_BranchOutcomeId" ON "Processes_DecisionRecords" ("BranchOutcomeId");

CREATE INDEX "IX_Processes_DecisionRecords_ProcessRunId_CreatedAtUtc" ON "Processes_DecisionRecords" ("ProcessRunId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_DecisionRecords_StepRunId" ON "Processes_DecisionRecords" ("StepRunId");

CREATE INDEX "IX_Processes_ImprovementCandidates_ProcessDefinitionId" ON "Processes_ImprovementCandidates" ("ProcessDefinitionId");

CREATE INDEX "IX_Processes_ImprovementCandidates_ProcessRunId" ON "Processes_ImprovementCandidates" ("ProcessRunId");

CREATE INDEX "IX_Processes_ImprovementCandidates_Status" ON "Processes_ImprovementCandidates" ("Status");

CREATE INDEX "IX_Processes_JournalEntries_ProcessRunId_OccurredAtUtc" ON "Processes_JournalEntries" ("ProcessRunId", "OccurredAtUtc");

CREATE INDEX "IX_Processes_JournalEntries_StepRunId" ON "Processes_JournalEntries" ("StepRunId");

CREATE INDEX "IX_Processes_RunAssignments_PartyId" ON "Processes_RunAssignments" ("PartyId");

CREATE INDEX "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_StepDefinitionId" ON "Processes_RunAssignments" ("ProcessRunId", "RoleRequirementId", "StepDefinitionId");

CREATE INDEX "IX_Processes_RunAssignments_RoleRequirementId" ON "Processes_RunAssignments" ("RoleRequirementId");

CREATE INDEX "IX_Processes_RunAssignments_StepDefinitionId" ON "Processes_RunAssignments" ("StepDefinitionId");

CREATE INDEX "IX_Processes_Runs_ProcessDefinitionId" ON "Processes_Runs" ("ProcessDefinitionId");

CREATE INDEX "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersionId" ON "Processes_Runs" ("ProcessDefinitionId", "ProcessDefinitionVersionId");

CREATE INDEX "IX_Processes_Runs_ProjectId" ON "Processes_Runs" ("ProjectId");

CREATE INDEX "IX_Processes_Runs_Status" ON "Processes_Runs" ("Status");

CREATE INDEX "IX_Processes_StepArtifactInputs_ArtifactExpectationId" ON "Processes_StepArtifactInputs" ("ArtifactExpectationId");

CREATE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId" ON "Processes_StepArtifactInputs" ("StepDefinitionId");

CREATE UNIQUE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId_ArtifactExpectationId" ON "Processes_StepArtifactInputs" ("StepDefinitionId", "ArtifactExpectationId");

CREATE INDEX "IX_Processes_StepArtifactInputs_StepDefinitionId_DisplayOrder" ON "Processes_StepArtifactInputs" ("StepDefinitionId", "DisplayOrder");

CREATE INDEX "IX_Processes_StepBranchOutcomes_StepDefinitionId_DisplayOrder" ON "Processes_StepBranchOutcomes" ("StepDefinitionId", "DisplayOrder");

CREATE UNIQUE INDEX "IX_Processes_StepBranchOutcomes_StepDefinitionId_Key" ON "Processes_StepBranchOutcomes" ("StepDefinitionId", "Key");

CREATE INDEX "IX_Processes_StepDependencies_DependsOnBranchOutcomeId" ON "Processes_StepDependencies" ("DependsOnBranchOutcomeId");

CREATE INDEX "IX_Processes_StepDependencies_DependsOnStepId" ON "Processes_StepDependencies" ("DependsOnStepId");

CREATE INDEX "IX_Processes_StepDependencies_StepDefinitionId" ON "Processes_StepDependencies" ("StepDefinitionId");

CREATE INDEX "IX_Processes_StepDependencies_StepDefinitionId_DisplayOrder" ON "Processes_StepDependencies" ("StepDefinitionId", "DisplayOrder");

CREATE UNIQUE INDEX "UX_ProcessStepDeps_Conditional" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId") WHERE "DependsOnBranchOutcomeId" IS NOT NULL;

CREATE UNIQUE INDEX "UX_ProcessStepDeps_Unconditional" ON "Processes_StepDependencies" ("StepDefinitionId", "DependsOnStepId") WHERE "DependsOnBranchOutcomeId" IS NULL;

CREATE INDEX "IX_Processes_StepRoleRequirements_RoleRequirementId" ON "Processes_StepRoleRequirements" ("RoleRequirementId");

CREATE UNIQUE INDEX "IX_Processes_StepRoleRequirements_StepDefinitionId_RoleRequirementId_ResponsibilityKind" ON "Processes_StepRoleRequirements" ("StepDefinitionId", "RoleRequirementId", "ResponsibilityKind");

CREATE UNIQUE INDEX "IX_Processes_StepRuns_ProcessRunId_Sequence" ON "Processes_StepRuns" ("ProcessRunId", "Sequence");

CREATE INDEX "IX_Processes_StepRuns_ProcessRunId_Status" ON "Processes_StepRuns" ("ProcessRunId", "Status");

CREATE INDEX "IX_Processes_StepRuns_SelectedBranchOutcomeId" ON "Processes_StepRuns" ("SelectedBranchOutcomeId");

CREATE INDEX "IX_Processes_StepRuns_StepDefinitionId" ON "Processes_StepRuns" ("StepDefinitionId");

CREATE INDEX "IX_Processes_WorkBriefs_ProcessRunId" ON "Processes_WorkBriefs" ("ProcessRunId");

CREATE INDEX "IX_Processes_WorkBriefs_StepRunId" ON "Processes_WorkBriefs" ("StepRunId");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413191821_AddProcessRuntimeForeignKeysAndDependencyUniqueness', '10.0.4');

BEGIN TRANSACTION;
DROP INDEX "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status";

ALTER TABLE "Processes_Definitions" ADD "NextVersionNumber" INTEGER NOT NULL DEFAULT 1;

CREATE UNIQUE INDEX "UX_ProcessVersions_DraftPerDef" ON "Processes_DefinitionVersions" ("ProcessDefinitionId", "Status") WHERE "Status" = 'Draft';

CREATE UNIQUE INDEX "UX_ProcessVersions_PubPerDef" ON "Processes_DefinitionVersions" ("ProcessDefinitionId") WHERE "Status" = 'Published';

CREATE INDEX "IX_Processes_Definitions_ActivePublishedVersionId" ON "Processes_Definitions" ("ActivePublishedVersionId");

CREATE INDEX "IX_Processes_Definitions_Id_ActivePublishedVersionId" ON "Processes_Definitions" ("Id", "ActivePublishedVersionId");

CREATE TABLE "ef_temp_Processes_Definitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_Definitions" PRIMARY KEY,
    "ActivePublishedVersionId" TEXT NULL,
    "AutonomyLevel" TEXT NOT NULL,
    "ConcurrencyToken" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "Criticality" TEXT NOT NULL,
    "CustomerName" TEXT NOT NULL,
    "GovernanceNotes" TEXT NOT NULL,
    "InterfaceContractSummary" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "NextVersionNumber" INTEGER NOT NULL DEFAULT 1,
    "OwnerName" TEXT NOT NULL,
    "ProjectId" TEXT NULL,
    "Slug" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "ValueStatement" TEXT NOT NULL,
    CONSTRAINT "FK_Processes_Definitions_Processes_DefinitionVersions_Id_ActivePublishedVersionId" FOREIGN KEY ("Id", "ActivePublishedVersionId") REFERENCES "Processes_DefinitionVersions" ("ProcessDefinitionId", "Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Processes_Definitions" ("Id", "ActivePublishedVersionId", "AutonomyLevel", "ConcurrencyToken", "CreatedAtUtc", "Criticality", "CustomerName", "GovernanceNotes", "InterfaceContractSummary", "Name", "NextVersionNumber", "OwnerName", "ProjectId", "Slug", "Status", "Summary", "UpdatedAtUtc", "ValueStatement")
SELECT "Id", "ActivePublishedVersionId", "AutonomyLevel", "ConcurrencyToken", "CreatedAtUtc", "Criticality", "CustomerName", "GovernanceNotes", "InterfaceContractSummary", "Name", "NextVersionNumber", "OwnerName", "ProjectId", "Slug", "Status", "Summary", "UpdatedAtUtc", "ValueStatement"
FROM "Processes_Definitions";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Processes_Definitions";

ALTER TABLE "ef_temp_Processes_Definitions" RENAME TO "Processes_Definitions";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Processes_Definitions_ActivePublishedVersionId" ON "Processes_Definitions" ("ActivePublishedVersionId");

CREATE INDEX "IX_Processes_Definitions_Id_ActivePublishedVersionId" ON "Processes_Definitions" ("Id", "ActivePublishedVersionId");

CREATE INDEX "IX_Processes_Definitions_ProjectId" ON "Processes_Definitions" ("ProjectId");

CREATE UNIQUE INDEX "IX_Processes_Definitions_Slug" ON "Processes_Definitions" ("Slug");

CREATE INDEX "IX_Processes_Definitions_Status" ON "Processes_Definitions" ("Status");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413200735_AddProcessDefinitionLifecycleInvariants', '10.0.4');

BEGIN TRANSACTION;
ALTER TABLE "Activity_Entries" ADD "IdempotencyKey" TEXT NULL;

CREATE TABLE "Processes_Outbox" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Processes_Outbox" PRIMARY KEY,
    "ProjectId" TEXT NULL,
    "ProcessDefinitionId" TEXT NULL,
    "ProcessRunId" TEXT NULL,
    "CommandKey" TEXT NOT NULL,
    "PayloadJson" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "AttemptCount" INTEGER NOT NULL,
    "LastAttemptAtUtc" TEXT NULL,
    "NextAttemptAtUtc" TEXT NULL,
    "CompletedAtUtc" TEXT NULL,
    "LastError" TEXT NOT NULL,
    "LeaseToken" TEXT NOT NULL,
    "LeaseExpiresAtUtc" TEXT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_Activity_Entries_IdempotencyKey" ON "Activity_Entries" ("IdempotencyKey");

CREATE INDEX "IX_Processes_Outbox_ProcessDefinitionId_CreatedAtUtc" ON "Processes_Outbox" ("ProcessDefinitionId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_Outbox_ProcessRunId_CreatedAtUtc" ON "Processes_Outbox" ("ProcessRunId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_Outbox_ProjectId_CreatedAtUtc" ON "Processes_Outbox" ("ProjectId", "CreatedAtUtc");

CREATE INDEX "IX_Processes_Outbox_Status_NextAttemptAtUtc_LeaseExpiresAtUtc" ON "Processes_Outbox" ("Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413204600_AddProcessOutboxDurableSideEffects', '10.0.4');

COMMIT;

BEGIN TRANSACTION;
DROP INDEX "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_StepDefinitionId";

CREATE UNIQUE INDEX "UX_ProcessStepRuns_RunStep" ON "Processes_StepRuns" ("ProcessRunId", "StepDefinitionId");

CREATE UNIQUE INDEX "UX_ProcessRunAssignments_RunScoped" ON "Processes_RunAssignments" ("ProcessRunId", "RoleRequirementId") WHERE "StepDefinitionId" IS NULL;

CREATE UNIQUE INDEX "UX_ProcessRunAssignments_StepScoped" ON "Processes_RunAssignments" ("ProcessRunId", "RoleRequirementId", "StepDefinitionId") WHERE "StepDefinitionId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260413230346_AddProcessRuntimeRowSingularity', '10.0.4');

COMMIT;

