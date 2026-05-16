# Current State

## Implemented Baseline

The prerequisite-boundaries implementation added:

- `IAgentContextContributor` and related request/result contracts,
- `MafAgentContextContributionProvider`,
- Workbench source snapshot provider,
- Process runtime evidence source provider,
- Workflow runtime evidence source provider,
- unit and integration tests for those boundaries.

The baseline solved the original coupling problem: Cognitive Memory no longer needs to be directly hardwired into private MAF context composition or read existing module persistence ad hoc.

## Remaining Friction

### Source paging

`WorkbenchProjectStructureSourceSnapshotProvider`, `ProcessRuntimeEvidenceSourceProvider`, and `WorkflowRuntimeEvidenceSourceProvider` all construct complete item lists before calling `MemorySourceSnapshotPage.Apply(...)`. That shape does not scale to future Cognitive Memory scans over many projects, process runs, workflow runs, and artifacts.

### Cursor semantics

`MemorySourceSnapshotPage.Apply(...)` finds the cursor by id and silently restarts at index 0 when the cursor is not found. That is unsafe for resumable ingestion because a stale cursor can duplicate items without an explicit trace or failure.

### Redaction and source hashes

Process and Workflow providers redact exposed content, but some hashes are computed from raw JSON payloads. That can be acceptable only if hash values are treated as restricted non-exportable integrity values. Cognitive Memory needs explicit contract semantics before storing or projecting them.

Workbench snapshots currently mark nodes as internal/read-only and include `Notes` directly. Future projection and context-pack rendering must not treat arbitrary Workbench notes as unrestricted.

### MAF context traces

`AgentContextContributionResult` carries trace metadata, but `MafAgentContextContributionProvider` maps the result to chat messages and discards trace metadata. Future Cognitive Memory must be able to prove why a context pack was injected, skipped, or failed.

### Architecture gate state

The Cognitive Memory architecture bundle records prerequisite closure, but its execution report still contains stale pending language. The hardening work should update architecture gate artifacts after implementation.
