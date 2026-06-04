# Semantic Invariants SB04

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed workflow input descriptors | shared AgentFramework model source | unit and integration proof | persisted on workflow definitions | failing-first transcript |
| Template metadata parsing | `inputParameters` YAML and loader source | `Load_parses_office365_watch_input_parameters` | seed version refreshes managed definitions | loader duplicate-key validation |
| Descriptor preservation | in-memory and persistent catalog source | `CatalogPreservesWorkflowInputParametersOnSaveAndStatusChange` | save/status/import paths carry descriptors | source assertions reject description-string parsing |
| Scheduler validation | schema service source | integration proof | DI registration plus `SavePlanAsync` enforcement | missing required email negative test |
| Raw JSON fallback | schema service source | raw array integration proof | no-descriptor workflows stay schedulable | typed schemas require object input |

## SB04-INV-TEMPLATE-PARAMETERS

- Invariant ID: `SB04-INV-TEMPLATE-PARAMETERS`
- Source raw note: R8 and R12.
- Expected behavior: Office365 email-watch templates expose durable `inputParameters` metadata for connection, email, project, parent node, processed category, and lookback interval.
- Disallowed shallow implementation: parsing human descriptions, hard-coding fields in the Scheduler UI, or keeping metadata only in tests.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`
- Passing test: `Load_parses_office365_watch_input_parameters` in `bundle://proof/SB04/transcripts/unit-template-catalog-schema-after-sb04.txt`
- Changed source files: `repo://Templates/Workflows/workflows/office365-email-watch-workflows.yaml` current SHA-256 `d06725736f2f224600135f138548c44a13cb63e0f5f411169397c3957cdb46f8`; `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` current SHA-256 `77635c06cb69dab5d9ae29133a8af5cf97cfcec0807088a7dc419c00e5cfdb19`
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`
- Red-team negative case: `HEAD` had no descriptors or `inputParameters`; loader validation rejects duplicate descriptor keys.
- Downstream dependency check: SB05 can render typed controls from descriptors instead of raw JSON.

## SB04-INV-DEFINITION-PRESERVATION

- Invariant ID: `SB04-INV-DEFINITION-PRESERVATION`
- Source raw note: R12.
- Expected behavior: workflow definitions preserve input descriptors when saved from templates, saved as new versions, status-changed, imported, and persisted.
- Disallowed shallow implementation: parsing descriptors from template YAML at Scheduler time only or dropping them after seed/save.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`
- Passing test: `CatalogPreservesWorkflowInputParametersOnSaveAndStatusChange` in `bundle://proof/SB04/transcripts/unit-template-catalog-schema-after-sb04.txt`
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs` current SHA-256 `1921192cffc8d59fe6727a225e7663331cb1a1129c69cb04c6b7844feeb5b554`; `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` current SHA-256 `be337a73d8b6e8ababb934e56d35b37f16b916de117ea3e761230478525d03f9`
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`
- Red-team negative case: status-change test fails if descriptors are not copied into the new version.
- Downstream dependency check: Scheduler can resolve schema from saved workflow definitions without reloading template files.

## SB04-INV-SCHEDULER-SCHEMA-VALIDATION

- Invariant ID: `SB04-INV-SCHEDULER-SCHEMA-VALIDATION`
- Source raw note: R8.
- Expected behavior: Scheduler resolves a selected workflow schema and rejects schedules missing required descriptor values.
- Disallowed shallow implementation: accepting invalid required fields and hoping workflow execution fails later.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`
- Passing test: `SavePlanAsync_rejects_missing_required_workflow_input_parameters` in `bundle://proof/SB04/transcripts/integration-scheduler-workflow-schema-after-sb04.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs` current SHA-256 `b6c1c8b1811dd0554614e0312c3b6072df8de1879a1221d78e61adf7edd729c5`; `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs` current SHA-256 `cf91303f42d6e3a3d9c0e41a622107a29ee1c5bebb16b4e81c17c490c67882a3`
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`
- Red-team negative case: missing `emailAddress` prevents schedule save.
- Downstream dependency check: SB05 UI can surface validation errors before scheduling.

## SB04-INV-RAW-JSON-FALLBACK

- Invariant ID: `SB04-INV-RAW-JSON-FALLBACK`
- Source raw note: R8 compatibility constraint.
- Expected behavior: workflows without descriptors still accept valid raw JSON input.
- Disallowed shallow implementation: forcing every workflow into typed-object input and breaking existing raw JSON workflows.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`
- Passing test: raw fallback assertion in `Workflow_input_schema_service_resolves_descriptors_defaults_and_raw_json_fallback`, captured by `bundle://proof/SB04/transcripts/integration-scheduler-workflow-schema-after-sb04.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs` current SHA-256 `b6c1c8b1811dd0554614e0312c3b6072df8de1879a1221d78e61adf7edd729c5`
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`
- Red-team negative case: typed descriptor workflows require JSON object input, while no-descriptor workflows accept raw array JSON.
- Downstream dependency check: SB05 can show raw JSON editor when `UsesRawJsonFallback` is true.

## SB04-INV-NORMALIZED-DEFAULTS

- Invariant ID: `SB04-INV-NORMALIZED-DEFAULTS`
- Source raw note: R8.
- Expected behavior: Scheduler validation normalizes default processed category and lookback interval into saved workflow input JSON when optional fields are omitted.
- Disallowed shallow implementation: displaying defaults in UI only while saving incomplete JSON that fails at runtime.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-missing-workflow-input-schema-before-implementation.txt`
- Passing test: `SavePlanAsync_persists_normalized_workflow_input_defaults` in `bundle://proof/SB04/transcripts/integration-scheduler-workflow-schema-after-sb04.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerWorkflowInputSchemaService.cs` current SHA-256 `b6c1c8b1811dd0554614e0312c3b6072df8de1879a1221d78e61adf7edd729c5`
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions-workflow-input-schema.txt`
- Red-team negative case: the integration test omits optional fields and asserts saved JSON contains `CanDoItAllProcessed` and integer `336`.
- Downstream dependency check: SB06/SB07 can assume Scheduler plans contain normalized workflow input defaults.
