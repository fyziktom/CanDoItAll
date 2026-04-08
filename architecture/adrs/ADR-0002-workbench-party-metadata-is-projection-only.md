# ADR-0002: Workbench Party Metadata Is Projection-Only

## Status

Accepted

## Context

The structure surface still needs short labels such as the linked directory party name, meeting party summary, and work-item assignee summary. Persisting ids or rich linked-party payloads in Workbench metadata makes those projection fields look canonical and encourages future dual-write bugs.

## Decision

- Workbench meeting, participant, and work-item metadata may store only display-side summaries for reusable parties.
- Backward-compatible JSON property names may remain when needed for safe payload reads, but their semantic meaning is projection-only.
- Canonical identity, role, and allocation data must remain in the assignment store.

## Consequences

- Structure preview and outline rendering may depend on summary strings but must load editor state from canonical assignments.
- Metadata cleanup can ignore stale legacy party ids because they are no longer part of the supported contract.
- Future metadata additions in this area should be framed as snapshot or display fields, not identity references.
