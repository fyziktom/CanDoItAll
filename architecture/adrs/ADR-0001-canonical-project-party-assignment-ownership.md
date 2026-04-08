# ADR-0001: Canonical Project-Party Assignment Ownership

## Status

Accepted

## Context

Project structure nodes can reference reusable parties such as customers, delivery units, managers, and work-item assignees. Earlier implementations duplicated that ownership state in Workbench metadata and CRM/HR assignment rows, which created split truth and drift during edits, deletes, and project transfers.

## Decision

- `CrmHr.ProjectPartyAssignment` is the only canonical persisted source for project-scoped and node-scoped party ownership.
- Workbench may read canonical assignments through the integration bridge and may trigger reconciliation during lifecycle mutations.
- Workbench metadata must not be treated as authoritative identity storage for reusable parties.

## Consequences

- Canonical ownership changes must go through the party-assignment bridge, not direct Workbench metadata edits.
- Delete and subtree-move flows must reconcile canonical assignments or compensate the Workbench mutation.
- Future features that need party ownership should extend the canonical assignment model or add a new explicit canonical store instead of reusing metadata.
