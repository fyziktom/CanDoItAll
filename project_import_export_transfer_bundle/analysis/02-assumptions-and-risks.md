# Assumptions And Risks

## Assumptions

- `all projects` means project portfolio data plus its project-structure/workbench records, not unrelated process runtime history, chat history, or every module table that merely has a nullable `ProjectId`.
- The database-to-database path should be implemented as a new `IDatabaseTransferHandler` so it appears alongside existing processes, agents, providers, and MCP-token transfer items.
- Zip import/export should be project-scoped and should not require creating a whole database snapshot profile.
- Existing IDs may be preserved during transfer/import because cross-table project and node references are ID-based.
- Replace-existing behavior may clear current target project data before import/transfer, matching the existing transfer handlers' default behavior.

## Critical Path Risks

- If the project handler omits workbench child tables, imported projects will appear on the board but lose structure, layout, bindings, media routes, or history.
- If project zip import uses different rules than database transfer, the two modes can drift and close only part of the user's request.
- If target clearing is ordered incorrectly, foreign-key constraints can fail or leave orphan rows.
- If managed-file/media references are packaged only as database strings and not as bytes, restored project nodes can reference missing media.
- If UI proof checks only the happy path visually, the transfer item could exist in one dialog but not the new-database prompt or vice versa.

## Validation Risks

- Existing test fixtures may need separate source and target SQLite profiles to prove `DatabaseTransferService` end to end.
- Browser proof needs a healthy watched app plus seed data or a synthetic profile set; missing seed data should be documented instead of pretending the control is proven.
- Zip import/export may need host-level proof by checking the created `.zip` path and imported record counts because browser download is not currently a pattern in this app.

## Reopen Triggers

- Any dependent UI proof shows `Projects` missing from either the data-sources transfer dialog or the startup/new-database transfer prompt.
- A project package import restores project cards but not structure nodes, links, bindings, references, projection layout, or view state.
- An integration test finds orphaned workbench records or duplicate-key failures after repeated import/transfer.
- A package export does not include media/storage payloads referenced by copied project nodes.
