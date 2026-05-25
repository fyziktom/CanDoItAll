# Normalized Requirements

| ID | Requirement | Owning subbundle | Proof method |
| --- | --- | --- | --- |
| PR-001 | Add a process-owned step completion finalizer used by every executor kind before process transition. | SB01 | Tests for direct agent and workflow-backed role paths. |
| PR-002 | Preserve the Processes vs Workflows boundary: workflows may execute roles, but Processes own artifact contracts and finalization. | SB01 | Source assertions and workflow-backed role test. |
| PR-003 | Replace “recorded expectation id exists” completion with validated artifact completion state. | SB02 | Artifact validation tests for missing, invalid, stale, wrong producer, and valid artifacts. |
| PR-004 | Add durable artifact projection diagnostics for required artifact failures. | SB02 | Tests asserting diagnostics are persisted and used by recovery/blocking. |
| PR-005 | Restrict response-text and auto-decision projections to compatible artifact modes. | SB02/SB04 | Negative tests where response text cannot satisfy evidence/deliverable expectations. |
| PR-006 | Make manager artifact recovery evidence-bound and structurally auditable. | SB03 | Recovery artifact provenance tests and blocked-insufficient-evidence test. |
| PR-007 | Require explicit manager/recovery capability instead of generic fuzzy `lead` fallback. | SB03 | Manager resolver negative tests. |
| PR-008 | Stop relying on shared mutable `DispatchCandidate` sets for completion state. | SB01/SB03 | Source assertions and tests using artifact ledger reload/result model. |
| PR-009 | Prevent placeholders, proxy records, or subprocess gap markers from satisfying required expectations. | SB04 | Placeholder/subprocess projection negative tests. |
| PR-010 | Detect repeated invariant artifact failures and switch to recovery/blocking instead of blind retries. | SB05 | Retry fingerprint tests and stranded step tests. |
| PR-011 | Keep all database changes PostgreSQL-only. | SB06 | Migration/model validation and residue audit. |
| PR-012 | Extend integration tests around current artifact matching coverage instead of replacing it. | SB06 | Focused test suite and final build proof. |
