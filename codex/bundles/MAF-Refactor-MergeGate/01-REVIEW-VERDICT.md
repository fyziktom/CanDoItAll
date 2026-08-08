# Review verdict

## Decision

**HEAD `79a6c0d7de353acfae3511e2671baf7daee2b498` is not merge-ready.**

The main MAF architecture is now materially improved and should be preserved. The remaining blockers
are concentrated in five boundaries:

1. persisted governance integrity;
2. module ownership of source authority and effective process policy context;
3. scope-aware durable process cleanup;
4. optional ordinary-conversation transaction/persistence correctness;
5. attempt-level lightweight-LLM usage and release proof.

## Already accepted

- narrow runtime ports and thin MAF composition;
- no direct product-module references from MAF;
- product-owned process recovery;
- canonical UI observation versus authority separation;
- unknown-source fail-closed behavior;
- per-run workspace services;
- runtime-state envelope v2 and inner-payload conversation detection;
- per-proposal approvals;
- provider-neutral lightweight LLM invocation;
- ordinary conversation projects outside MAF.

## Merge rule

No partial merge. Merge only after SB09 records a clean Release build, blocker tests, full regression
comparison, architecture guards, application smoke evidence, exact SHA, and a `MERGE READY` decision.
