# 04-policy-preserving-operations

## Status

- `Ready`

## Objective

Preserve explicit operator policy through all Cognitive Memory validation operations.

## Required Edits

- Store access level, risk level, and allow-restricted flag on probe sessions.
- Reuse stored policy when asking probe turns.
- Add policy fields or policy snapshots to relevant operation audit records.
- Add warnings when restricted source truth is excluded.

## Closure Proof

- Restricted probe session can recall restricted source truth when explicitly allowed.
- Project-only probe session cannot recall restricted source truth.
