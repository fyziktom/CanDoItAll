# Database Table Inventory

The current EF model maps **39 tables**. Clone/snapshot, migration parity, and legacy-upgrade validation must treat this as the baseline inventory.

| Table | Source |
| --- | --- |
| `Activity_Entries` | `src/CanDoItAll.Modules.Activity/ActivityModels.cs` |
| `Factory_PromptBlocks` | `src/CanDoItAll.Modules.Factory/FactoryDomain.cs` |
| `Factory_PromptBlueprints` | `src/CanDoItAll.Modules.Factory/FactoryDomain.cs` |
| `Factory_PromptBuildSessions` | `src/CanDoItAll.Modules.Factory/FactoryDomain.cs` |
| `Factory_PromptFlowTemplates` | `src/CanDoItAll.Modules.Factory/FactoryDomain.cs` |
| `Factory_PromptRunNodes` | `src/CanDoItAll.Modules.Factory/FactoryDomain.cs` |
| `Factory_PromptRuns` | `src/CanDoItAll.Modules.Factory/FactoryDomain.cs` |
| `Infrastructure_BackgroundJobRecords` | `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs` |
| `Infrastructure_SearchDocuments` | `src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs` |
| `Projects_ProjectHierarchyLinks` | `src/CanDoItAll.Modules.Projects/ProjectModels.cs` |
| `Projects_ProjectOptionSelections` | `src/CanDoItAll.Modules.Projects/ProjectModels.cs` |
| `Projects_ProjectPhases` | `src/CanDoItAll.Modules.Projects/ProjectModels.cs` |
| `Projects_Projects` | `src/CanDoItAll.Modules.Projects/ProjectModels.cs` |
| `Prompts_PromptArtifactTags` | `src/CanDoItAll.Modules.Prompts/PromptModels.cs` |
| `Prompts_PromptArtifacts` | `src/CanDoItAll.Modules.Prompts/PromptModels.cs` |
| `Prompts_PromptCollections` | `src/CanDoItAll.Modules.Prompts/PromptModels.cs` |
| `Prompts_PromptTags` | `src/CanDoItAll.Modules.Prompts/PromptModels.cs` |
| `Prompts_PromptUsageRecords` | `src/CanDoItAll.Modules.Prompts/PromptModels.cs` |
| `Prompts_PromptVersions` | `src/CanDoItAll.Modules.Prompts/PromptModels.cs` |
| `Resources_ProjectResources` | `src/CanDoItAll.Modules.Resources/ResourceModels.cs` |
| `Security_SecretRecords` | `src/CanDoItAll.Modules.Security/SecurityModels.cs` |
| `Security_SecretReferences` | `src/CanDoItAll.Modules.Security/SecurityModels.cs` |
| `TestLab_TestCases` | `src/CanDoItAll.Modules.TestLab/TestLabModels.cs` |
| `TestLab_TestEvidence` | `src/CanDoItAll.Modules.TestLab/TestLabModels.cs` |
| `TestLab_TestPlans` | `src/CanDoItAll.Modules.TestLab/TestLabModels.cs` |
| `TestLab_TestRuns` | `src/CanDoItAll.Modules.TestLab/TestLabModels.cs` |
| `Validation_Checklists` | `src/CanDoItAll.Modules.Validation/ValidationModels.cs` |
| `Validation_Findings` | `src/CanDoItAll.Modules.Validation/ValidationModels.cs` |
| `Validation_Runs` | `src/CanDoItAll.Modules.Validation/ValidationModels.cs` |
| `Workbench_ProjectObjectLinks` | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` |
| `Workbench_ProjectObjects` | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` |
| `Workbench_ProjectStructureLeases` | `src/CanDoItAll.Modules.Workbench/ProjectStructureAgentModels.cs` |
| `Workbench_ProjectStructureOperationAnalytics` | `src/CanDoItAll.Modules.Workbench/ProjectStructureAgentModels.cs` |
| `Workbench_ViewStates` | `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` |
| `Workspace_ProjectStructureAgentProfiles` | `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationModels.cs` |
| `Workspace_ProjectStructureAgentProjectOverrides` | `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationModels.cs` |
| `Workspace_ProjectStructureAgentSettings` | `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationModels.cs` |
| `Workspace_ProviderProfiles` | `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` |
| `Workspace_Settings` | `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` |

## Notes

- The current model shows very few explicit EF relationships, which makes provider-agnostic logical export/import feasible.
- Secret and provider tables live inside the selected application database, so they move with clone/snapshot operations but cannot host the control-plane catalog.
- Runtime switching proof should seed at least one record in more than one module table so cross-profile data isolation is obvious.
