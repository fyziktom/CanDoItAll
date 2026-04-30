# 02 — Typed Recovery Decision and Rework Packets


## Problem

Recovery is currently represented mainly as text. The system needs typed recovery decisions and typed rework packets so agents can finish partial work efficiently and safely.

## Tasks

1. Add `AgentRecoveryMode`, `AgentRecoveryDecision`, `AgentContextStrategy`, and validation models.
2. Add `AgentReworkPacket`, `AgentReworkFinding`, `AgentReworkArtifactRef`, `AgentToolReceiptRef`, `AgentProofRequirement`, and `AgentReusableProofRef`.
3. Add JSON serialization/deserialization tests.
4. Add validators for required fields, failure categories, artifacts, proof requirements, and prohibited actions.
5. Persist recovery decisions and packets in a durable location: process step attempt metadata, execution run metadata, or a dedicated journal table/record.
6. Update prompt construction so rework packets are included as compact JSON, not just prose.

## Acceptance criteria

- Actual files/classes exist in the snapshot.
- Recovery decisions are generated before retry/rework.
- Rework packets are persisted and linked to the source execution run.
- A rework prompt contains the packet id and JSON payload.
- Invalid packets fail validation before agent execution.

## Suggested tests

- `AgentRecoveryModelsTests.Rework_packet_round_trips_json`
- `AgentRecoveryDecisionTests.Missing_failure_category_is_invalid`
- `ProcessRunAutomationDispatchServiceTests.Qa_rejection_creates_rework_packet`

