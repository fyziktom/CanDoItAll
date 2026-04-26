# Artifact And Recovery Inventory

## Artifact Sources

- AgentFramework execution artifacts from `ExecutionRunDetail.Artifacts`.
- Deterministic mock artifact projection from `SerializedSessionStateJson`.
- Response text auto-projection for durable text artifacts.
- Completed decision artifact projection for eligible governed decision artifacts.
- Manual process artifact recording from the UI.

## Artifact Sinks

- `ProcessArtifactRecord` with process run, step run, expectation ID, kind, title, trust, sensitivity, managed storage path, and external reference key.
- Storage placement through `IStoragePlacementService`.
- Process Workspace evidence tab and runtime read query.

## Recovery Sources

- Dispatcher retry loop with max attempts.
- Fresh chat recovery attempts.
- Provider repair recovery.
- AgentFramework startup recovery for interrupted runs.
- Process run recovery worker for active runs with ready, waiting, or in-progress agent-owned steps.

## Missing Recovery Concepts

- User-visible recovery package.
- User-visible previous attempt summary.
- User-visible missing artifact instructions.
- Explicit manual rerun command for failed/blocked agent steps.
- Process health event for outbox dead-letter.
