# Assumptions And Risks

## Assumptions

- `ProjectPartyAssignment` is the canonical owner for node-scoped participant, meeting, and work-item party relationships.
- Workbench metadata remains valuable as a derived projection for previews, labels, and existing UX copy.
- The smallest safe boundary is to extend the existing project-facing bridge instead of introducing a new cross-module service layer.

## Critical Path Risks

- Legacy data that only populated metadata and never wrote assignments may appear unlinked after the read path is tightened.
- If lifecycle reconciliation is patched only in Workbench and not exposed through the bridge, the module boundary will age badly.

## Validation Risks

- Browser proof may still be blocked by the known Playwright MCP environment issue, which must be retried and documented honestly if it persists.

## Risk Handling In This Bundle

- Prefer assignment-first editor reads and assignment-first writes so any projection drift becomes non-canonical.
- Add explicit bridge operations for node-assignment replacement, node-delete cleanup, and subtree-transfer reassignment.
- Add lifecycle tests before trusting the browser pass.
- Re-run the architecture review after implementation and record any remaining non-trivial structural risk instead of hiding it.

## Reopen Triggers

- Reopen `01` if the structure-page editor still initializes from metadata or if browser proof shows stale selected-party state.
- Reopen `02` if delete or subtree-transfer tests leave stale canonical assignments behind.
- Reopen `03` if the post-fix architecture review still reports the dual-write issue as unresolved or if browser proof regresses.
