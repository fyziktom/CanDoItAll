# Lean execution contract

## Branch safety

1. Confirm branch `providers-shared`.
2. Record current HEAD in BR00 `RESULT.md`.
3. Inspect commits after audited HEAD `fdf1ff9702c376ad0ffd101a34d6bf542c9857d2` when present.
4. Never reset, force-push, discard, or overwrite unrelated user changes.
5. Keep `codex/bundles/shared-providers/**` read-only until BR08.

## Document discipline

For each subbundle, read only:

- `DECISION-LOCK.md`
- `TARGET-BOUNDARY.md`
- `EXECUTION-CONTRACT.md`
- current subbundle `README.md`
- previous `RESULT.md`

Do not create:

- proof manifests
- per-file hashes
- repeated source inventories
- duplicated architecture snapshots
- generated summaries of every root document
- multiple result/status documents for one subbundle

Create exactly one `<subbundle>/RESULT.md` after its code gate. Keep it under 150 lines unless a compiler/test failure needs exact diagnostics.

## RESULT format

```markdown
# BRxx result

- Status: DONE | BLOCKED
- Start HEAD: ...
- End HEAD: ...

## Implemented
- ...

## Boundary decisions applied
- ...

## Validation
- `exact command` — PASS/FAIL

## Compatibility
- Schema/API/behavior notes

## Remaining items
- only items owned by a later subbundle or an exact blocker
```

Do not paste long logs. Include the first actionable error and the path to any full local log only when needed.

## Command budget per subbundle

Default maximum:

- one restore
- two affected-project builds
- three targeted test commands
- one architecture-guard command
- one EF command

A single additional repair build/test is allowed after a concrete compile or test failure. Do not repeatedly run commands without source changes.

Use:

- `--no-restore` after the first successful restore
- `--no-build` for test reruns after a successful build
- the narrowest affected solution/project set before BR07
- one discovered canonical solution path, recorded in BR00

Do not use `--list-tests` unless a filter returns zero tests or the target test name genuinely cannot be found by source search. Use it at most once.

## Infrastructure

Docker and Podman commands are prohibited. Do not start, stop, build, inspect, or retry containers. Tests requiring container lifecycle are deferred and recorded once in BR08.

Local non-container EF model checks are allowed when they do not require external infrastructure.

## Edit scope

- Do not mass-format.
- Do not change unrelated nullable annotations, naming, UI styling, or logging.
- Do not update package versions.
- Do not implement original SB07.
- Preserve public API and persistence compatibility.
- Source comments must be in English.

## Buildable checkpoints

Every subbundle commit must:

- compile all directly affected projects
- pass the subbundle's targeted tests
- have no unresolved conflict markers or generated junk
- pass `git diff --check`
- satisfy all prior subbundle guards

Do not commit a knowingly temporary dependency inversion. A temporary compatibility bridge may exist only when:

- it is outside Workspace ownership,
- it is explicitly removed by a named later subbundle,
- a guard is added in that later subbundle,
- it does not create a second committed canonical owner.

## Commit policy

One commit per completed subbundle:

`BRxx: <concise outcome>`

The commit should include its single `RESULT.md`. Do not create documentation-only follow-up commits unless correcting a factual error.

## Failure policy

- Fix ordinary compiler/test defects within the command budget.
- For a semantic conflict with `DECISION-LOCK.md`, follow the lock and document the conflict.
- For an external blocker, record the exact command and first actionable error once; do not loop.
- A blocked subbundle stops later code subbundles. BR08 may still record the handoff state without pretending acceptance.
