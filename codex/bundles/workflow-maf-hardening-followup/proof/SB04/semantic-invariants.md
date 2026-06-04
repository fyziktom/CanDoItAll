# SB04 semantic invariants

## SB04-CHECKPOINT-METADATA-CONTRACT

- Invariant ID: `SB04-CHECKPOINT-METADATA-CONTRACT`
- Source raw note: R5 requires checkpoint models and a trusted storage abstraction.
- Expected behavior: workflow checkpoints are represented by strongly typed ids, checkpoint kind, trust-boundary state, resume availability, optional node/request identity, private payload reference, and timestamps.
- Disallowed shallow implementation: using stringly typed checkpoint ids, storing opaque checkpoint blobs directly on events, or leaving checkpoint state inaccessible to runtime/API consumers.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-checkpoint-tests.txt` shows the checkpoint model and store API were absent.
- Passing test: `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt` proves the in-memory store saves/lists checkpoint metadata.
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions-checkpoints.txt` verifies `IWorkflowCheckpointStore`, `WorkflowCheckpointRecord`, and API-facing checkpoint properties exist.

## SB04-RUNTIME-CAPTURE-BOUNDARIES

- Invariant ID: `SB04-RUNTIME-CAPTURE-BOUNDARIES`
- Source raw note: R4 requires superstep/request events to be usable as checkpoint lifecycle evidence.
- Expected behavior: the in-process MAF backend captures checkpoint metadata at completed, failed, and waiting-for-input lifecycle boundaries; the waiting checkpoint keeps the pending external request id and node id.
- Disallowed shallow implementation: only defining a store with no runtime writes, or recording waiting state without linking it to the pending request.
- Passing test: `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt` asserts completed and waiting HITL workflows persist the expected checkpoint kind and request identity.
- API proof: `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt` shows route `api/workflows/test-runs` returns completed and waiting checkpoint metadata with request identity.

## SB04-RESUME-AVAILABILITY-IS-EXPLICIT

- Invariant ID: `SB04-RESUME-AVAILABILITY-IS-EXPLICIT`
- Source raw note: resume must be implemented minimally or explicitly blocked with clear API/UI state.
- Expected behavior: metadata-only in-process checkpoints carry `WorkflowResumeAvailability.NotSupported` and a durable-backend resume message.
- Disallowed shallow implementation: silently ignoring resume, exposing a resume affordance without state, or claiming production durability from in-process metadata capture.
- Passing test: `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt` and `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt` assert `NotSupported` resume state.
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit-checkpoints.txt` verifies no workflow runtime path uses native checkpoint blob loading or placeholder resume code.

## SB04-CHECKPOINT-TRUST-BOUNDARY

- Invariant ID: `SB04-CHECKPOINT-TRUST-BOUNDARY`
- Source raw note: checkpoint storage is a private infrastructure trust boundary and must not load untrusted blobs.
- Expected behavior: in-process checkpoints use a private metadata payload marker and do not store raw workflow input, executor output, secrets, or uploaded checkpoint blobs.
- Disallowed shallow implementation: accepting user-uploaded checkpoint state, exposing raw checkpoint payloads in normal API/UI, or using checkpoint storage as an artifact dump.
- Documentation proof: `repo://docs/workflow-maf-hardening.md` states the checkpoint trust boundary, raw blob restrictions, and `NotSupported` resume semantics.
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions-checkpoints.txt` and `bundle://proof/SB04/transcripts/anti-stub-audit-checkpoints.txt` verify metadata-only payload reference and absence of raw input payload references.

## SB04-PERSISTENT-CHECKPOINT-STORAGE

- Invariant ID: `SB04-PERSISTENT-CHECKPOINT-STORAGE`
- Source raw note: initial trusted storage must work for tests and production persistence without claiming durability from preview execution.
- Expected behavior: `InMemoryWorkflowRunStore` supports checkpoint tests; `PersistentWorkflowRunStore` persists checkpoints through `AgentFramework_WorkflowCheckpoints`; PostgreSQL migrations include the table and indexes.
- Disallowed shallow implementation: only adding an in-memory queue with no persistent model, or leaving the EF model changed without a migration.
- Passing build: `bundle://proof/SB04/transcripts/solution-build-slnx-after-checkpoints.txt` proves the migration and model compile.
- Source assertions: `bundle://proof/SB04/transcripts/source-assertions-checkpoints.txt` verifies persistent entity and migration coverage.

- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`, migration files, and tests listed in `bundle://proof/SB04/manifest.md`.
- Red-team negative case: `bundle://proof/SB04/transcripts/anti-stub-audit-checkpoints.txt` verifies no runtime path loads user-controlled native checkpoint blobs or exposes raw checkpoint payloads.
- Downstream dependency check: `bundle://proof/SB04/transcripts/integration-workflow-api-checkpoints-after-implementation.txt`, `bundle://proof/SB04/transcripts/component-workflows-page-smoke-after-checkpoints.txt`, and `bundle://proof/SB04/transcripts/solution-build-slnx-after-checkpoints.txt` passed.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `WorkflowCheckpointRecord` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `bundle://proof/SB04/transcripts/unit-workflow-foundation-checkpoints-after-implementation.txt` | `bundle://proof/SB04/transcripts/anti-stub-audit-checkpoints.txt` |
| PostgreSQL checkpoint table | `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260529111314_AddWorkflowCheckpoints.cs` | `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | `bundle://proof/SB04/transcripts/solution-build-slnx-after-checkpoints.txt` | `bundle://proof/SB04/transcripts/source-assertions-checkpoints.txt` |
