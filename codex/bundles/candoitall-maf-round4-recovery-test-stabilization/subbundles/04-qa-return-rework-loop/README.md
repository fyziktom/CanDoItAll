# 04 — QA Return and Rework Loop


## Problem

QA rejection should not force a full step redo. It should generate a typed rework packet and route to a repair step that preserves completed work.

## Tasks

1. Identify where QA outcomes are represented in `ProcessStepOutcomeResult` and process branches.
2. Convert QA rejection findings into `AgentReworkFinding` objects.
3. Attach artifact refs, failed proof receipts, reusable proof refs, minimal next actions, and prohibited actions.
4. Ensure repair agents inspect referenced artifacts directly.
5. Ensure repair output includes repair notes and proof receipts.
6. Ensure QA recheck consumes the repair packet/result and verifies corrections.

## Acceptance criteria

- QA rejection creates a durable `AgentReworkPacket`.
- Repair step prompt receives compact packet JSON.
- Repair step does not re-bootstrap/rewrite everything unless packet says it is necessary.
- QA recheck can trace each finding to repair evidence.

## Suggested tests

- `ProcessReworkIntegrationTests.Qa_rejection_routes_to_repair_with_packet`
- `ProcessReworkIntegrationTests.Repair_step_preserves_existing_artifacts_and_reruns_invalidated_proofs`

