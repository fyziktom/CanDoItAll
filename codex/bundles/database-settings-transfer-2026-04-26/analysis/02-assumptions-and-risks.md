# Assumptions And Risks

## Assumptions

- The local DataProtection key ring is shared by databases in the same installation, so encrypted ProjectStructure tokens and Security secret payloads can be copied between database profiles on the same machine.
- "Processes" means process definitions and related design-time records, not active runs, step runs, launch history, or outbox events.
- "AI agents" means the sandbox workspace catalog records owned by AgentFramework storage, while provider profile data remains a separate transfer item.
- New database creation in UI includes the Workspace data-sources management flow and the main layout managed-SQLite creation flow.

## Critical Path Risks

- If the generic transfer abstractions open the wrong database context, every handler can copy into the active runtime database instead of the selected target. This makes the transfer foundation a critical subbundle.
- If module-specific handlers are placed in the wrong project, the solution can gain reference cycles or hidden dependencies.
- If secret payloads are mishandled, the feature could expose token material or create unusable encrypted data.
- If the UI prompt is only added to one creation path, the raw "new db in ui" requirement is only partially satisfied.

## Validation Risks

- Existing DB profiles on the developer machine may not contain every item group, so tests should cover handler behavior with seeded contexts instead of relying only on live data.
- Browser proof requires a runnable CanDoItAll Web app and a reachable database-management route.
- Agent catalog transfer depends on file-backed workspace roots, so it needs file-system proof or focused unit coverage.

## Reopen Triggers

- Reopen the foundation subbundle if any handler needs direct access to the current scoped `AppDbContext` for source/target transfer.
- Reopen Workspace handlers if ProjectStructure token transfer shows cleartext token in UI/logs or fails after runtime DB switch.
- Reopen UI if the modal lacks a source database selector, item checkboxes, or the new-database transfer prompt.
- Reopen validation if browser analytics do not include the open dialog state and a screenshot review.
