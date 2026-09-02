# SB05 — Merge Development Into UI Refactoring

**Status:** Blocked until SB01; ideally execute after SB02/SB04 branches are known  
**Outcome:** Current development is merged into the original UI branch with auditable decisions  
**Proof tier:** Governed

## Repository / branch

```text
CanDoItAll/ui-refactoring
```

Do not create the merge on v2.

## Prerequisites

- clean/protected worktree,
- refreshed development and original branch tips,
- v2 denylist passes,
- selected package version `V` is recorded,
- final or candidate Components/FileTools integration SHAs are known.

## Merge

```bash
git checkout ui-refactoring
git merge --no-ff origin/development
```

Do not rebase. Do not use `ours` or `theirs` at repository scope.

## Conflict policy

Follow `analysis/02-branch-and-conflict-map.md`.

Required outcomes:

- `.gitignore`: current development plus `.idea/`,
- `global.json`: current development SDK policy,
- `package.json`: current scripts plus root `watch`,
- `App.razor`: current development plus Material Symbols asset,
- Podman guide: useful content moved into current operations docs; stale root file removed.

Inspect every conflict semantically. Generated CSS must be regenerated from its owning Tailwind
source, never resolved by choosing an arbitrary side.

## Post-merge audit

```bash
git diff --check
git grep -n "<<<<<<<\|=======\|>>>>>>>"
```

Run the v2 scope guard.

Inspect the merge commit parents; one must be the pre-merge original branch and one must be the
selected development tip.

## Initial validation

- restore/build product graph in source mode,
- run documentation validation if Podman docs were already migrated,
- do not run the entire stable suite yet.

## Acceptance

- merge commit exists,
- no conflict markers,
- current development SDK and behavior are preserved,
- original valid deltas remain,
- v2 guard passes,
- product graph compiles or all remaining failures are explicitly assigned to SB06.

## Progression gate

A focused merge-resolution diff and parent SHAs are recorded in the execution report.

## Reopen triggers

- development moves before closure,
- original branch gains new commits,
- merge includes a v2 unique commit,
- unresolved compile failures are not Components-integration related.
