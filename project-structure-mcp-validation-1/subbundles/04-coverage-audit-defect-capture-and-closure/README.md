# Coverage audit, defect capture, and closure

## Status

- `Completed`

## Objective

- Close the validation honestly by auditing raw-note coverage, collecting checklist and analytics evidence, recording any remaining defects, and running the final bundle gate.

## Covered Inputs

- `N004`
- `N005`
- `N006`

## Prerequisites

- `03-live-mcp-import-shaping-and-repair-loop` completed

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\traceability\01-requirement-traceability.md`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAnalyticsService.cs`

## Deliverables

- Checklist and analytics evidence for the executed validation
- Final raw-note closure table
- Explicit defect capture for every unresolved issue
- Final bundle validator pass or an honest blocked state

## Dependency Impact

- Produces the final review package that later bundle or product work will rely on
- If this phase finds weak earlier proof, it must reopen the earlier subbundle rather than paper over the gap

## Validation Depth

- Standard
- Requires explicit raw-note closure and final validator alignment before the bundle may close

## Implementation Steps

1. Query the project-structure checklist for the created validation workspace or shaped project areas.
2. Capture analytics evidence for the MCP operations, using the HTTP API directly if the MCP surface still lacks an analytics tool.
3. Record any missing MCP surface or unresolved behavior as a defect in the bundle and validation workspace.
4. Compare the shipped result against every raw note and update the closure table.
5. Update the bundle status, subbundle statuses, execution report, and residual risks.
6. Run the completed-stage bundle validator and repair anything it rejects before closure.

## Scope Exceptions

- If analytics are only reachable through the HTTP API, record that explicitly as a surface-gap finding even if the underlying analytics data can still be retrieved.

## Do Not Do

- Do not mark the validation complete while any raw note still lacks proof.
- Do not hide missing analytics or checklist proof inside a vague residual-risk paragraph.

## Acceptance Checklist

- Checklist evidence exists for the created live structure.
- Analytics evidence exists for the validation run, or the lack of MCP analytics surface is explicitly captured as a defect.
- Every raw note is marked `Solved`, `Partially solved`, or `Not solved` with proof.
- The completed-stage bundle validator passes or the bundle is explicitly marked blocked with the reason.

## Proof Required

- Checklist query output
- Analytics query output or explicit missing-surface defect proof
- Updated execution report with final subbundle rows, browser analytics rows, and raw-note closure rows
- Successful `validate_bundle.py --stage completed` output

## Browser Validation Logging

- Route: final validation workspace structure route and any defect-review route needed for the final screenshot set
- Viewports: large desktop, medium-width only if final review reveals layout-sensitive gaps
- Actions: final navigation, route confirmation, screenshot capture, and visual review answers
- Evidence paths: record final screenshot paths in `reviews/01-execution-report.md`
- Visual review questions: confirm the created structure remains visible and coherent after all shaping and defect capture work

## Progression Gate

- Checklist and analytics proof are captured or honestly blocked with explicit defect records
- Raw-note closure is complete
- The final bundle validator passes before the bundle is marked complete

## Suggested Agent Prompt

```text
Implement this subbundle only. Audit the delivered validation against every raw note, capture checklist and analytics evidence, record any remaining MCP defect explicitly, update the bundle state, and close only after the final validator passes.
```
