# Trouble Log And Follow-Up Mapping

| Trouble ID | Area | Observed issue | Impact | Evidence | Recommended fix | Follow-up requirement | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| TRB-01 | Host startup | No-build/production-like local host failed serving `_framework/blazor.web.js`; Development startup from the web project worked. | Validation can look broken even when app code is fine. | `proof/host/web-run-stdout.log` | Add static asset startup diagnostics and a supported validation host runbook. | ARCH-01 | Recorded |
| TRB-02 | Database profile clarity | Runtime profile was a configured PostgreSQL override and needed extra proof to understand active storage. | Operators can validate against the wrong database if the active source is unclear. | `proof/api/clean-active-status.json` | Expose active profile source, database name, and override reason in status/UI. | ARCH-02 | Recorded |
| TRB-03 | Transfer completeness | Project/workbench data transfers, but external file payload/data manifest transfer is not first-class. | File-backed source truth cannot be cleanly replayed into validation storage. | `proof/api/database-transfer-preview.json` | Add file/data manifest transfer with hashes, redaction, and skip reasons. | ARCH-03 | Recorded |
| TRB-04 | Restricted source truth | Default consolidation scanned 0 project-structure items because restricted content was excluded. | A realistic validation run silently misses the source truth unless the operator knows to enable restricted policy. | `proof/api/consolidation-run-1.json` | Add explicit restricted-source warnings and policy-preserving run controls. | ARCH-04 | Recorded |
| TRB-05 | Budget continuation | Restricted consolidation created 80 candidates and stopped before evaluating all source items. | Long source-truth sets need resumable continuation, not one-shot runs. | `proof/api/consolidation-run-2-restricted.json` | Add cycle IDs, cursors, continuation metrics, and long-run orchestration. | ARCH-05, ARCH-10 | Recorded |
| TRB-06 | Dream quality | Dream aggregate candidates were source-mapped but too generic after redaction and were rejected. | Human approval cannot keep aggregates that do not carry concrete facts. | `proof/api/dream-aggregate-controlled-rejections.json` | Improve aggregate generation and gate structural-only candidates. | ARCH-06 | Recorded |
| TRB-07 | Probe policy | Probe start accepted restricted policy, but probe turns reconstructed Project-only policy. | Probe validation cannot trust restricted source-truth checks. | `proof/api/probe-turn-restricted-ask.json` | Persist and reuse the session policy on probe turns. | ARCH-07 | Recorded |
| TRB-08 | Probe vector recall | Probe turn recall did not pass projection options and reported `vector:projection-options-missing`. | Probe validation misses Qdrant behavior even after projection succeeds. | `proof/api/probe-turn-restricted-ask.json` | Pass projection collection/profile/embedding options through probe asks. | ARCH-08 | Recorded |
| TRB-09 | Qdrant diagnostics | Qdrant projection succeeded only after explicit options were supplied. | Operators need clearer defaults and health diagnostics. | `proof/api/qdrant-projection-rebuild.json`, `proof/api/qdrant-recall-ai-tap-source-truth-summary.json` | Add default projection profile diagnostics and vector-stage status summaries. | ARCH-09 | Recorded |

## Follow-Up Bundle

- Path: `codex/bundles/cognitive-memory-realistic-validation-architecture-hardening`
- Status: prepared as an implementation-ready architecture follow-up.
