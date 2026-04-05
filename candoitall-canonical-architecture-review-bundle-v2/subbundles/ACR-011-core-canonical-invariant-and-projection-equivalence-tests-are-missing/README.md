# ACR-011 — Core canonical-invariant and projection-equivalence tests are missing

- Severity: **High**
- Skill source: `canonical-model-review`
- Category: Testability drift
- Phase: **Phase 0**
- Timing: **Now**
- Dependencies: Foundational for every other finding.

## Problem statement

The repo has meaningful tests, but it lacks a dedicated ring that protects truth ownership, relation invariants, node-kind registry behavior, node-assignment integrity, node-evolution history, and multi-projection equivalence during architectural stabilization.

## Why this matters now

Without this test ring, the next waves of canonical refactor will be guesswork.

## Deliverables

- Architectural guardrail test ring
- Invariant tests, registry tests, actor-assignment integrity tests, projection-equivalence tests, node-evolution history tests

## Likely files touched

- `tests/CanDoItAll.Tests.Integration/*`
- `tests/CanDoItAll.Tests.Unit/*`
- `tests/CanDoItAll.Tests.Components/*`
