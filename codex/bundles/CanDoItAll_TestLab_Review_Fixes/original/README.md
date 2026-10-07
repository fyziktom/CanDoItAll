# TestLab UI decoupling — Codex execution handoff

**Entry point:** [prompt.md](prompt.md). Read the whole extracted directory, not only the standalone prompt.

**Decision:** the earlier Collaboration corrections are source-supported and no longer block selecting the next module. Extract the complete existing TestLab workspace. First close the narrowly identified shared conversation-shell disposal race in a separate logical change; do not turn this into another chat/module rewrite.

Reviewed repository: `fyziktom/CanDoItAll`  
Reviewed branch: `components-decoupling`  
Reviewed HEAD: `97989b9d13b9a6fa16280da98ec5b005a2f968c7`  
Review date: 2026-09-28  
Execution base: the user's **actual current checkout**, not the review SHA.

## Read order

1. [Execution prompt](prompt.md), then the current repository's instructions, UI seam guidance, testing guide and CI workflow.
2. [Collaboration closure and remaining shell prerequisite](COLLABORATION_REVIEW.md).
3. [TestLab source review, boundaries and consequences](TESTLAB_REVIEW_NOTES.md).
4. [Validation matrix](VALIDATION_MATRIX.md) and [measurement protocol](DEV_LOOP.md).
5. [Bundled shared v3 brief](shared/prompt.md), its four architecture documents and [shared validation guidance](shared/VALIDATION.md).

[Source index](SOURCES.md), [machine-readable source register](sources.json), [bundle metadata](bundle.json) and [package-only validation](PACKAGE_VALIDATION.md) provide provenance. The 22 shared-v3 files are copied unchanged. Its older source audit is historical; this module's review and the current code supersede stale observations, not the canonical repository rules.

## Scope in one sentence

Keep TestLab persistence and project ownership where they are; give its real renderer a lightweight contract boundary and a representative database-free sandbox while preserving editor state, admitted project lifetimes, child identities and honest save outcomes.

No new runner, evidence upload, TestLab HTTP rewrite, database migration, universal state framework, Processes/Workbench extraction, push or merge is authorized. Signed local implementation commits are allowed under the existing policy. The task is implementation plus verification, not another plan-only response.

## Evidence status

This handoff was prepared through source inspection and package checks. No application build, C# test, browser journey or performance measurement was executed by the reviewer. The recorded Collaboration total of 82 passing cases belongs to the previous implementation's report; its raw local artifacts were not available. The current merge HEAD must be validated by the executor.
