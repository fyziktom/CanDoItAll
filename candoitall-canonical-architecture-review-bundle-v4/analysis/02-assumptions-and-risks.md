# Assumptions And Risks

## Working Assumptions

- The existing `v3` repair is the baseline and must not regress.
- The same app database context type still backs both Workbench and CRM/HR persistence, but a single shared transaction is not assumed to be safely available through the current module contracts.
- Display-only metadata summaries may remain if they stop carrying canonical-looking identifiers.

## Critical Path Risks

- A rushed attempt at full universal-node redesign would expand scope and weaken closure.
- Changing cross-module contracts without focused tests could break the repaired structure-page flows.
- Playwright MCP is still likely blocked by the machine-level `EPERM` directory issue.

## Validation Risks

- Browser proof may still require fallback to Playwright test-runner evidence plus screenshots if MCP remains blocked.
- Metadata cleanup can silently affect preview/describer surfaces if summary fields are not preserved.

## Reopen Triggers

- Any regression in `ProjectStructurePartyPickerTests`, lifecycle integration tests, or the targeted Playwright structure flow reopens the relevant subbundle.
- Any bundle contract drift requires rerunning prepared-stage validation before execution continues.
