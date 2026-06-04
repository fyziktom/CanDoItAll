# Proof Manifest SB04

Status: `Completed`

Subbundle: `04-scheduler-workflow-input-contract-and-template-parameter-schema`

## Owned Requirements

- R8: Scheduler can select a workflow and validate typed input fields for email/contact, project, parent node, processed category, and interval/lookback values.
- R12: file-backed templates expose durable metadata that the loader and saved workflow definitions preserve.

Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Manifest

Source hash transcript: `bundle://proof/SB04/transcripts/changed-file-hashes-sb04.txt`

| Path | Before marker | Current SHA-256 |
| --- | --- | --- |
| `repo://Templates/Workflows/manifest.yaml` | HEAD blob `759b937375a476ed3e2f11c73c0cf6696670e7f7` | `03ba447e60f1a6304cdaf7b1ce6929c6282aa652811dcf7a487449a6fed7c863` |
| `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` | `NEW_IN_WORKTREE` | `d06725736f2f224600135f138548c44a13cb63e0f5f411169397c3957cdb46f8` |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowInputParameterModels.cs` | `NEW_IN_WORKTREE` | `6dcced6268156e4d98b0df2518816dad36d1d1bb526ccd81a89a657e54de53b7` |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs` | HEAD blob `59503ab2a3936ef65c7fd3caf16a805e78538f0d` | `4db6f9132a3abe869d210a2f0349bb98910c24366a6d6820b1aafb39d6eea401` |
| `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowCatalogModels.cs` | HEAD blob `fdfc84d16f11910d91774000f9e64f2a4bfe577e` | `c4ea6a0cc2a3757a11fc007377d5b992fa20072ddb54a775a98109aa1183ba3b` |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs` | HEAD blob `6a12c47b93169a86f3f8226c74731e7f94fbe31f` | `1921192cffc8d59fe6727a225e7663331cb1a1129c69cb04c6b7844feeb5b554` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` | HEAD blob `46e84a72c63257d8cf642c5d505241827aee0343` | `77635c06cb69dab5d9ae29133a8af5cf97cfcec0807088a7dc419c00e5cfdb19` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs` | HEAD blob `6be48f86288d3a92b553fa56655405a95be0f076` | `9b23de113b80ef43fd256966c10e487d631582d3bf896274fb0e5f4c91ceba48` |
| `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | HEAD blob `cbc2892eed55a863d32977ed1d5d404655205f65` | `be337a73d8b6e8ababb934e56d35b37f16b916de117ea3e761230478525d03f9` |
| `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs` | HEAD blob `69bad4b2600ec91dc1a8247ea3ed8228a1892f2c` | `144e649a67664d8051f7604d057be541a55ab3c835f69eb35c3c2664c15dd7fa` |
| `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs` | HEAD blob `1b464a8542f16eba73fe6dfc26ea1a19b70e2b2d` | `cf91303f42d6e3a3d9c0e41a622107a29ee1c5bebb16b4e81c17c490c67882a3` |
| `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModuleServiceCollectionExtensions.cs` | HEAD blob `8fe3c1c881e53a1b03a3ca2d036d2b9242c92d69` | `2e239987da9bccac21a436e7bb2b3a95ab3d6bd2f3aa87ee6f4e680316ddf1d7` |
| `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs` | `NEW_IN_WORKTREE` | `b6c1c8b1811dd0554614e0312c3b6072df8de1879a1221d78e61adf7edd729c5` |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs` | HEAD blob `462309e2ad7df4579e900d55dfc47ac66f40fded` | `7b011a61b68d7f249b15588c57fb1678ca7ba57907fc235ac596dbfd3f9be7a5` |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs` | HEAD blob `a5d8393c0020b335a3683fd84099a19a02fbedae` | `b0d69491f099ec0379ed9b4cdb4fe7b6a2c41d1c87aefe4e7bd42c997d7283c9` |
| `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs` | HEAD blob `6255618dd00362040e81aa4582e130f9dc06b0ea` | `a8104a2ca2a9ce167ad32218cc6c4e134e6bce850bb299f8fb5642503ef9a44d` |

## Command Transcripts

- Failing-first: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`
- Passing scheduler schema proof: `bundle://proof/SB04/transcripts/integration-scheduler-workflow-schema-after-sb04.txt`
- Build: `bundle://proof/SB04/transcripts/build-after-sb04.txt`
- Unit tests: `bundle://proof/SB04/transcripts/unit-template-catalog-schema-after-sb04.txt`
- Scheduler integration tests: `bundle://proof/SB04/transcripts/integration-scheduler-workflow-schema-after-sb04.txt`
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit-workflow-input-schema.txt`
- Semantic invariant labels: `bundle://proof/SB04/transcripts/semantic-invariant-evidence.txt`

## Failing-First And Passing Proof

- Failing-first proof shows `HEAD` had no workflow input parameter descriptors, Scheduler schema service, or `inputParameters` template metadata.
- Passing unit proof covers YAML metadata parsing, in-memory catalog descriptor preservation, and existing template graph integrity.
- Passing integration proof covers persistent schema resolution, required-parameter rejection in `SavePlanAsync`, normalized defaults, and raw JSON fallback for workflows without descriptors.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowInputParameterModels.cs` defines strongly typed descriptor, kind, option-source, and option models.
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` parses `inputParameters` and validates duplicate keys.
- `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` and `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs` preserve descriptors across save, status changes, and imports.
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs` resolves workflow schema, validates required values, applies defaults, and preserves raw JSON fallback.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed workflow input descriptors | `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowInputParameterModels.cs` | loader/unit proof | saved on `WorkflowDefinition.InputParameters` | failing-first transcript |
| Template `inputParameters` metadata | `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` | `bundle://proof/SB04/transcripts/unit-template-catalog-schema-after-sb04.txt` | manifest seed version bumped for refresh | duplicate-key loader validation |
| Saved workflow descriptor preservation | catalog and persistent store source assertions | `CatalogPreservesWorkflowInputParametersOnSaveAndStatusChange` | seed service passes descriptors into definitions | status/import source assertions |
| Scheduler schema validation | `ISchedulerWorkflowInputSchemaService` | integration proof | registered in Scheduler module DI | required email negative save test |
| Raw JSON fallback | schema service source assertion | integration proof with `[1,2,3]` input | existing workflows with no descriptors remain schedulable | invalid typed object shape rejection |

## Browser, Host, And External Service Proof

- Browser proof is not required in SB04 because no visible Scheduler rendering changed. SB05 owns typed-form UI proof.
- Live Office365 and CRM option lookup are intentionally deferred; SB04 records option-source metadata without introducing broad Scheduler dependencies.

## Anti-Stub Audit

`bundle://proof/SB04/transcripts/anti-stub-audit-workflow-input-schema.txt` reports no `TODO`, `NotImplemented`, `throw new NotImplementedException`, or fixture-specific branches in scoped workflow schema production files.
