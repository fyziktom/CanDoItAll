# SB01 Branch Hygiene Inventory

## Decision

- The development...maf-processes-refactor branch baseline contains $baselineDeletedCount deleted historical codex/bundles/* paths.
- Those deletions are classified as accidental merge-risk churn because SB01 is not allowed to remove unrelated historical proof bundles.
- The deleted historical bundle paths were restored from development into the working tree. They are intentionally left unstaged so the user's index remains under user control.
- The new previous decoupling bundle and this follow-up bundle remain in scope for the branch.

## Diff Classification

| Category | Current treatment | Evidence |
| --- | --- | --- |
| Runtime source/tests/docs/solution | Preserved for follow-up implementation | $runtimeChangedCount changed entries in branch baseline outside codex/bundles |
| Historical codex/bundles deletions | Restored from development; not an intentional deletion | $baselineDeletedCount deleted entries in branch baseline; $restoredUntrackedCount restored historical paths visible as untracked workspace files |
| Previous MAF/Processes decoupling bundle | Preserved as added branch evidence | epo://codex/bundles/maf-processes-decoupling-bundle-v1/reviews/01-execution-report.md |
| Current hardening follow-up bundle | Preserved as active execution contract | undle://README.md |

## Progression Decision

- SB02 may start after SB01 closure because the accidental historical bundle deletions are no longer absent from the filesystem and the production MAF hidden-dependency scan/build proof passed.
