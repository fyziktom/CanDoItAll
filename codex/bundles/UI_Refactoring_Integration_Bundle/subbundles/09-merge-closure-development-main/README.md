# SB09 — Merge Closure To Development And Main

**Status:** In progress — SB08 closed; final refresh, local commits and owner handoff underway
**Outcome:** Owner-ready merge sequence and final governed report  
**Proof tier:** Governed

## Default write policy

Prepare local merge-ready commits and instructions. Do not push, publish, or merge protected
remote branches unless the owner explicitly authorizes it in the invoking session.

## Pre-closure checks

1. refresh all remotes,
2. ensure candidate branches did not move unexpectedly,
3. rerun v2 scope guard on CanDoItAll integration HEAD,
4. rerun version consistency,
5. verify clean worktrees,
6. verify no unreviewed generated diffs,
7. attach proof summary.

## Upstream order

1. Components repair/version branch -> Components `main`; require green CI.
2. FileTools compatibility/version branch -> FileTools `main`; require green CI.
3. Rebase/merge neither upstream branch from CanDoItAll. Instead update CanDoItAll exact source
   pins if final upstream SHAs changed.
4. Re-run focused source proof after pin change.

## CanDoItAll canonical order

```text
ui-refactoring -> development -> main
```

Do not merge `ui-refactoring` directly to main in parallel.

Before `ui-refactoring -> development`:

- update integration branch with any final development movement,
- resolve only new conflicts,
- rerun focused tests and v2 guard.

After merge to development:

- require development CI green,
- verify original UI integration commit is an ancestor,
- verify v2 head and every v2 unique commit are not ancestors.

Then merge development to main and require main CI green.

## Required ancestry proof

Record outputs equivalent to:

```bash
git merge-base --is-ancestor <original-ui-final> <development-final>
git merge-base --is-ancestor <development-final> <main-final>
! git merge-base --is-ancestor origin/ui-refactoring-v2 <development-final>
! git merge-base --is-ancestor origin/ui-refactoring-v2 <main-final>
```

Also run the full dynamic v2 denylist against development and main.

## Final report

Complete:

- baseline versus final SHAs,
- selected `V`,
- branch/merge graph,
- changed files by repository,
- approval review,
- tests and browser/container proof,
- skips,
- residual risks,
- deferred v2 statement,
- exact owner actions remaining.

## Acceptance

- all critical/high readiness items closed,
- no v2 commit in development/main,
- exact upstream pins match final green commits,
- canonical ancestry is proven,
- no unauthorized remote write occurred,
- execution report is complete.

## Completion marker

Set bundle status to `Implemented — awaiting owner merge` or `Merged` only when supported by the
recorded remote state.
