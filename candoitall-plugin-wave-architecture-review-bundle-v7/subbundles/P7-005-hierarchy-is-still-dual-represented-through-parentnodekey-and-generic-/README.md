# P7-005 - Hierarchy is still dual-represented through ParentNodeKey and generic link rows

- Severity: High
- Gate: Hard blocker
- Status: Open
- Repeated from: PW6-007

## Problem

Create, seed, reparent, and subtree move flows still write canonical parent assignment and also persist Contains/BelongsTo link rows for the same hierarchy. Even though user-authored generic hierarchy links are forbidden, the storage model still duplicates the tree in two places.

## Required direction

Choose one canonical containment model for editable nodes. Prefer ParentNodeKey (or a dedicated canonical tree table) as the single truth. Keep the generic relation table for semantic edges only: DependsOn, Blocks, Uses, Validates, Tests, DerivedFrom, and similar non-containment semantics.

## Closure proof

Editable create/reparent/seed/move flows no longer persist hierarchy links; guardrail tests fail if Contains/BelongsTo is reintroduced for canonical editable nodes.
