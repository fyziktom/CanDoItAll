# 03 Typed Recovery Decision Taxonomy

## Goal

Replace text-only retry classification with a durable typed decision model.

## Tasks

1. Add `AgentRecoveryMode` enum.
2. Add `AgentRecoveryDecision` DTO.
3. Add `IProcessRecoveryDecisionService`.
4. Classify invalid structured output, missing finalizer, missing required tool/proof, failed proof, provider unavailable, policy blocked, QA rejected, retry budget exhausted, and outbox dead-lettered.
5. Persist recovery decisions as journal events and expose them to UI view models.
6. Tests must assert decision mode/category/reason for each case.

## Acceptance criteria

- Dispatcher no longer relies only on a text recovery directive for routing.
- Recovery decisions are queryable and visible.
