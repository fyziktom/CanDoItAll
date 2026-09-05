# Prerequisites, invalidation and recovery

SB01 is the source/behavior foundation. SB02 depends on its route/lazy-load/context baseline. SB03 depends on workspace ownership and query contracts. SB04 depends on catalog/host target and result contracts. SB05 depends on editor session/section lifetime. SB06 depends on tested implementation slices; SB07 depends on complete behavior coverage.

## Before a phase

Record source SHA/dirty state, sibling SHAs/mode, relevant changed files, exact requirement/matrix rows, selected fully qualified tests/data cases and derived expected count. Build fresh owning projects/solutions before any no-build test. Review current repository testing/CI instructions.

Check branch drift by impact, not SHA equality alone. Do not rebase, merge or reset unrelated user work as a prerequisite ritual. Reconcile changed source with this plan and reopen affected characterization.

## Invalidation rules

| Change | Reopen / rerun |
|---|---|
| Current route/state/context mapping or page load triggers | SB02; route, two lazy-history cases, selected chat-context and page composition |
| Catalog selection/requested-open/repair/team/chat/results | SB03 and dependent editor host cases |
| Target identity, section/session, reset, mutable copy, disposal | SB04 and SB05 stale/command cases |
| Save normalization, version, capability mutation or commit/refresh semantics | SB05 policy/real operation/adapter/component/host cases |
| Public DTO owner, constructor/DI registration, descendant service or project/assets | Dependency audit; affected actual composition/subtree tests; final stable/browser proof if composition changed |
| Test helper/selector/replacement | Rediscover exact affected cases and audit behavior coverage |
| Any production/test edit after final portability scan | Regenerate scan, review deltas and enforce without baseline write |
| Any final artifact no longer tied to current source | Invalidate affected proof/hash links; rerun only the affected gates plus mandatory final checks |

The final stable aggregate is required once for this planned UI DI/composition change. Repeat it after final proof only if later changes invalidate it; do not run the full suite after every phase.

## Recovery

Keep a clean phase checkpoint/diff and exact baseline oracles. Roll back an incomplete source slice to its known-good checkpoint without discarding unrelated edits; re-run its dependent flows before continuing. Prefer small reversible source moves with immediately migrated tests.

Source rollback cannot reverse committed database/provider/capability writes. Use isolated/disposable application fixtures for mutation proof, record created IDs, and follow existing cleanup semantics. Cancellation or refresh failure must never trigger a blind write retry. Record a committed-but-not-refreshed outcome and recover the read separately.

An existing defect, missing runtime prerequisite or scope-crossing dependency blocks only affected work until resolved/documented. It is not permission to weaken an acceptance row or label incomplete behavior proven.
