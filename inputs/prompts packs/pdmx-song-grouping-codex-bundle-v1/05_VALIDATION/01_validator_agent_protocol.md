# Validator Agent Protocol

The validator agent runs **after** Codex implementation.

## Validator mission

Determine whether the implementation is:
- architecturally aligned,
- operationally safe,
- test-covered,
- ready for copied real-DB evaluation,
- or needs a post-implementation fix phase.

## Validator stages

### Stage 1 — Static diff audit
Check:
- target files actually changed in the expected areas
- no surprising unrelated rewrites
- no reintroduction of destructive regroup logic
- no canonical-truth-in-tags anti-pattern

### Stage 2 — Schema audit
Check:
- membership table exists
- run preview tables exist
- profile table exists
- indexes exist
- migration is coherent
- compatibility with `SongGroupId` preserved or intentionally replaced safely

### Stage 3 — Test audit
Check:
- unit tests added
- integration tests added
- Playwright coverage expanded where appropriate
- failure-path tests exist

### Stage 4 — Runtime smoke checks
Check on temp DB/copy:
- app boots
- migrations apply
- profile refresh works
- dry run works
- apply flow works on safe sample
- UI pages load

### Stage 5 — Copied real-DB audit
Check:
- original DB untouched
- copied DB workflow documented and used
- suspicious cluster reporting exists
- metrics and sample audit artifacts produced

## Required validator outputs

- verdict:
  - `pass`
  - `pass with cautions`
  - `needs post-implementation phase`
- issue list with severity:
  - critical
  - high
  - medium
  - low
- exact impacted files or features
- next-step prompt if additional implementation phase is needed
