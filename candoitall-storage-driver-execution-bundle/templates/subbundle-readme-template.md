
# {{SUBBUNDLE_TITLE}}

## Status

- `Ready for implementation`

## Objective

- State the exact implementation outcome for this subbundle.

## Covered Inputs

- List the raw notes, normalized requirements, and workbook touchpoints owned here.

## Prerequisites

- List the upstream subbundles, migrations, fixtures, or proof that must already exist.

## Exact Source References

- Add the exact repository paths the implementer must read before editing code.

## Deliverables

- List the concrete code, tests, migrations, UI artifacts, and report updates expected from this subbundle.

## Dependency Impact

- Explain what downstream phases depend on this subbundle and why weak proof here would invalidate them.

## Validation Depth

- State the closure depth, for example `Critical foundation`, `Critical UI foundation`, or `End-to-end regression and closure`.

## Implementation Steps

1. Read the required source references and workbook rows.
2. Implement the subbundle in the exact order needed to keep the repo buildable.
3. Run the targeted commands and capture proof.
4. Update the execution report and workbook/traceability artifacts before claiming closure.

## Scope Exceptions

- List any explicitly deferred or blocked work that is not allowed to disappear silently.

## Do Not Do

- Do not skip required tests, migrations, screenshots, or execution-report updates.
- Do not fake provider support or UI proof.
- Do not remove compatibility seams before the owning touchpoints are migrated.

## Acceptance Checklist

- Every owned requirement and touchpoint has observable implementation proof.
- Required build/test/browser evidence exists.
- Downstream progression gates remain valid.

## Proof Required

- Command output paths or summaries
- Screenshot paths and written screenshot findings for UI work
- Execution-report updates
- Honest blocked-status notes when proof could not run

## Browser Validation Logging

- Target route(s):
- Required viewports:
- Required Playwright MCP actions:
- Required screenshot paths:
- Required screenshot review questions:
- Use `N/A` only when the subbundle is not browser-visible.

## Progression Gate

- State the exact condition that must be true before downstream work may continue.

## Suggested Agent Prompt

```text
Implement the current subbundle only.
Read its source references, workbook touchpoints, and proof rules first.
Update the execution report as you go.
Do not skip Playwright MCP proof for UI changes.
```
