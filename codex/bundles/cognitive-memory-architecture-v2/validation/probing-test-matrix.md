# Interactive Memory Probing Test Matrix

## Functional Tests

| Area | Test |
|---|---|
| Session | Start, close, resume, and list probe sessions by project. |
| Manual question | User question creates turn, recall request, trace, answer, and metadata. |
| Trace linking | Probe turn stores recall trace id and context pack id. |
| Feedback | Confirm/correct/missing/wrong-scope/request-source actions persist. |
| Review | High-risk correction creates review item and does not update active memory. |
| Regression | Failed turn creates draft regression test with constraints. |
| Question generation | Generator uses coverage gaps, stale records, contradictions, and serendipity budget. |
| Epistemic evidence | Failed probes publish `KnowledgeGapEvidenceRef`. |
| Learning validation | Post-learning probe can update coverage evidence. |

## Non-Happy Path Tests

| Area | Test |
|---|---|
| Truth mutation | User correction cannot directly overwrite approved memory. |
| Scope confusion | Production Docker query using test Docker source is marked wrong-scope. |
| Overconfidence | High-confidence rejected answer creates calibration evidence. |
| Missing source | Answer without required source refs is marked needs-source-review. |
| Redaction | Secret-like text is redacted before external provider context. |
| Qdrant outage | Probe still works through lexical/graph fallback. |
| Access policy | Unauthorized probe cannot reveal restricted memory/source text. |
| Duplicate feedback | Repeated same feedback is idempotent or versioned, not duplicated silently. |
| Regression replay | Replay result links to new recall trace and does not mutate memory. |

## Browser Evidence

Capture:

- Dialogue Workbench start session.
- Answer with trace/source/confidence side panel.
- Correction action and created review item.
- Failed Docker context-separation probe.
- Draft regression test created from probe failure.
- Epistemic Drive evidence view showing probe-derived gap evidence.
