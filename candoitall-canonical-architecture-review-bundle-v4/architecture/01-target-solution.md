# Target Solution

## Target Boundary

- `CrmHr.ProjectPartyAssignment` remains the canonical source of truth for node-scoped responsible-party links.
- `Workbench` may persist only display-oriented party summaries in node metadata.
- The Workbench lifecycle methods own compensation if downstream canonical reconciliation fails.
- The Workbench bridge should express canonical node scope with a typed node-reference value instead of a raw `string`.

## Explicit Non-Goals

- No full Workbench node-family split in this wave.
- No broad rewrite of the CRM/HR generic assignment editor.
- No database-schema migration unless implementation proves one is unavoidable.
