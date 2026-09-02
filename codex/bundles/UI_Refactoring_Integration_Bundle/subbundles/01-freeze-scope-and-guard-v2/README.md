# SB01 — Freeze Scope And Guard V2

**Status:** Completed — 2026-09-02
**Outcome:** A reproducible baseline and a mechanical prohibition against v2 contamination  
**Proof tier:** Governed

## Scope

- all three repository identities, tips, remotes, and worktree states,
- exact original/v2 branch names,
- dynamic list of commits unique to v2,
- baseline artifact directory,
- package feed inventory.

## Non-goals

- no source edits,
- no merges,
- no package version changes,
- no snapshot updates.

## Steps

1. Read repository instructions.
2. Verify the sibling workspace layout.
3. Fetch all remotes with prune.
4. Record:
   - `git status --short --branch`,
   - current branch and HEAD,
   - remote URLs,
   - branch tips,
   - merge bases,
   - SDK and Node versions,
   - configured NuGet sources.
5. In CanDoItAll, generate:
   - original branch unique commits,
   - v2 unique commits relative to original,
   - current development unique commits relative to original.
6. Run `scripts/verify-scope.ps1` or `.sh` against the current original branch.
7. Save results under an ignored:
   `.artifacts/ui-refactoring-integration/sb01/`.
8. Update the execution report.

## Required commands

```powershell
./scripts/verify-scope.ps1 -MainRepoRoot ../CanDoItAll
```

Run from this bundle root, or adapt the path.

## Acceptance

- actual original branch is confirmed as `ui-refactoring`,
- actual forbidden branch is confirmed as `ui-refactoring-v2`,
- original unique commit count and identities are recorded,
- v2 denylist is non-empty and saved,
- no v2 unique commit is an ancestor of the original branch,
- all worktrees are either clean or pre-existing changes are explicitly protected.

## Progression gate

Proceed only when the execution report contains the baseline table and the scope script exits
successfully.

## Reopen triggers

- force-push on either UI branch,
- development branch movement before SB05,
- unexpected `ux-refactoring*` branches,
- dirty worktree ownership ambiguity.

## Execution result

Passed on CanDoItAll `77dcdc4c05ec0bc0f338744852a773f27c161a48`. All three worktrees
were clean at entry. Remote tips match the recorded baselines. FileTools was refreshed
over HTTPS without changing its configured SSH remote after SSH authentication failed.
The guard accepted the integration HEAD and rejected the forbidden branch identity.
Its single-result SHA indexing bug was repaired and full 40-character SHA evidence was
verified. See `../../proof/SB01/manifest.md` and the root execution report.
