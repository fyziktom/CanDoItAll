# Assumptions And Risks

## Working Assumptions

- The prerequisite-boundaries implementation is the accepted baseline.
- This bundle is a small refactor/hardening pass, not the Cognitive Memory implementation.
- Query-backed paging can be introduced incrementally per provider without changing public behavior.
- Cursor result shape can evolve in a backward-compatible way if tests and call sites are updated together.
- Redaction/hash semantics should be explicit in the contracts before durable memory stores source hashes.

## Critical Path Risks

- If source providers continue to materialize all rows before paging, Cognitive Memory backfills will be slow and memory-heavy.
- If stale cursors silently restart, incremental ingestion may duplicate records or produce false rebuild signals.
- If Workbench notes enter projection as unrestricted content, sensitive project details may be embedded or injected into agent context.
- If raw sensitive payload hashes are exported or projected, hashes could become policy-sensitive metadata with unclear handling.
- If MAF contributor traces are dropped, future recall/context bugs will be difficult to audit.

## Validation Risks

- Current tests use small fixtures and can pass while provider paging remains non-scalable.
- Cursor tests can miss deletion/reordering scenarios unless invalid and stale cursor cases are explicit.
- Redaction tests can prove common secrets are removed while still allowing unrestricted Workbench notes.
- Trace capture tests can check metadata exists but fail to prove it is connected to the injected messages or provider outcome.

## Reopen Triggers

- Any provider still loads all rows for unbounded scans after this bundle closes.
- Invalid cursors still restart from the first item without an explicit error or stale-cursor result.
- Workbench source snapshots expose arbitrary notes without sensitivity/redaction metadata.
- Sensitive source payload hashes are used in exportable metadata or projection payloads.
- Cognitive Memory MAF integration cannot retrieve contributor ids, statuses, trace metadata, and context-pack trace ids.
