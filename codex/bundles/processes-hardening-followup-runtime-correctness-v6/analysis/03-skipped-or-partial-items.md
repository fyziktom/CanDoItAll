# Suspected Skipped or Partial Items From Previous Bundle

This section is intentionally explicit so Codex does not mark the next bundle complete with a shallow implementation.

## Partially done

- Persisted operation contract fields: implemented, but strictness/compatibility policy is still weak.
- Operation-aware tool policy: implemented, but ledger semantics and script side-effect manifest are still weak.
- Trusted grounding ledger: emitted, but not yet authoritative in policy.
- Artifact lineage identity: typed lineage exists, but prove identity hash persistence and dedupe behavior.
- Storage-backed validation: implemented via workspace filesystem reader, but not storage-service backed.
- Workflow/subprocess adapters: projection exists, but explicit output mapping is missing.
- Typed blocked/failed lifecycle: typed fields exist, but routing is not executable enough.

## Must not be skipped again

- Alias overlap regression where the same alias exists in both writable and read-only lists.
- Manual/API transition validation parity with finalizer validation.
- Own-output artifact failure vs upstream-input materialization distinction.
- Script side-effect manifest and post-execution diff audit.
- Refactoring checkpoints to prevent `ProcessRunAutomationDispatchService` and tool policy from becoming unmaintainable.
