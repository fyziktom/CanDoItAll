# 04 - QA Return Rework Loop and Finding Propagation

## Problem

The process can branch to repair paths, but QA findings are not yet propagated as a durable typed repair contract.

## Required implementation

When a QA/checking step rejects work or selects a repairs-required branch:

1. Extract structured QA findings.
2. Create an `AgentReworkPacket` linked to the source implementation step and QA step.
3. Include target artifacts/files, failed proof receipts, reusable proofs, and minimal next actions.
4. Attach packet id to the repair step input/metadata.
5. Require repair step output to reference packet id in evidence refs or metadata.
6. QA recheck should inspect the packet and repair receipts.

## Repair prompt rule

The repair agent should be told:

```text
You are continuing an existing implementation. Do not regenerate the entire application or repeat completed work unless the rework packet explicitly requires it. Make the smallest change that resolves the findings, then rerun the invalidated proof tools.
```

## Acceptance criteria

- QA finding becomes a typed packet finding.
- Repair step receives target artifacts and prohibited actions.
- Repair step can reuse valid proof receipts and rerun invalidated proof receipts.
- QA recheck can reference the packet.

## Tests

- Simulate QA rejection with one finding and one artifact; assert packet creation.
- Assert repair prompt contains packet id and minimal-delta instruction.
- Assert repair output evidence refs include packet id.

## Execution status

Completed. QA and proof-failure recovery paths create typed packets containing findings, artifact refs, failed receipts, proof requirements, minimal actions, and prohibited actions.
