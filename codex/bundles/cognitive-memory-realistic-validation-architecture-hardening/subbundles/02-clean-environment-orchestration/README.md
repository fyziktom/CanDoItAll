# 02-clean-environment-orchestration

## Status

- `Ready`

## Objective

Make clean PostgreSQL and Qdrant validation environments repeatable, visible, and safe.

## Required Edits

- Expose active database profile origin, database name, and override source in Cognitive Memory status.
- Add Qdrant collection readiness and collection list diagnostics.
- Add idempotent clean validation profile creation guidance and proof capture.

## Closure Proof

- API proof shows the active clean profile and Qdrant readiness.
- UI proof shows operators can tell which profile is active.
