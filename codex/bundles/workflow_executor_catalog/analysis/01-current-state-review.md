# Current State Review

## What Codex improved

- MAF package line is now upgraded to `1.8.0`.
- Dynamic graph compilation still uses `WorkflowBuilder` and `BindAsExecutor`, which is acceptable for user-authored editable graphs.
- `HumanInput` no longer blocks a run just because the graph contains a human node. It now creates a `WorkflowExternalRequestRecord` only when the node is reached.
- Approval-required executors can generate external approval requests through `WorkflowExternalRequestApprovalGate`.
- Runtime events now use `WorkflowEventPayloadEnvelope` and `MafWorkflowEventNormalizer`.
- Checkpoint records exist and are persisted. In-process checkpoints are intentionally metadata-only and non-resumable.
- Payload/artifact policy is centralized in `WorkflowPayloadPolicyService`.
- Backend catalog honesty is improved: non-registered durable backends are planned/unavailable, not silently runnable.
- Plugin observer composition was introduced.

## Remaining high-risk observations

### Validator/catalog risk

`WorkflowDefinitionValidator` can validate executor registration and `CanExecute`, but only when constructed with `IWorkflowExecutorCatalog`. Current DI registrations explicitly create it without the catalog. That means workflows using unknown/planned/unavailable executor IDs may not be rejected during save/import/publish. Runtime will still fail later, but authoring-time validation becomes weaker.

This should be fixed before adding more executors.

### Artifact payload persistence risk

`WorkflowPayloadPolicyService` creates artifact metadata records and storage paths, but the reviewed model and service path do not show an actual content writer. This is dangerous because event payloads may be truncated inline and claim an artifact reference exists, while no payload content is retrievable.

Codex must prove a real writer exists or add one.

### Executor catalog usability gap

Current implemented built-in executors:
- `storage.file`
- `source.ingest`
- `http.fetch`
- `spreadsheet`
- `project-structure`
- `image.generate`

Current planned built-in executors:
- `json.transform`
- `markdown.render`
- `utility.delay`
- `human.approval`
- `command.process`

The planned executors are not optional polish. They are common workflow building blocks.

### Workspace/local file gap

`storage.file` can list/stat/read/write/append/search/diff. `source.ingest` can load file/folder candidates and optionally absolute paths. However users will expect folder lifecycle and file manipulation operations. The next bundle must make this explicit instead of hiding it inside source ingestion.

### Non-executor helper node ambiguity

`WorkflowNodeKind` includes `Artifact`, `AgentStep`, `Subworkflow`, `StrictLogic`, and `Triage`. Current compiler special-cases `LlmCall`, `Executor`, and `HumanInput`; other kinds are pass-through unless represented as executor nodes. This is too ambiguous for user-authored workflows and should be normalized.
