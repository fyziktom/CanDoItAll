# 04-validation-and-proof

## Status

- `Completed`

## Objective

Run final validation for the component package, sandbox UI, and MCP server; capture browser analytics; close every raw note; and synchronize bundle status.

## Covered Inputs

- N001 through N012
- Requirement R011 plus final proof for R001 through R010

## Prerequisites

- Subbundle 01 closure gate passed.
- Subbundle 02 closure gate passed with browser proof.
- Subbundle 03 closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\mermaid_wrapper_bundle\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\mermaid_wrapper_bundle\traceability\01-requirement-traceability.md`
- `C:\repositories\CanDoItAll\mermaid_wrapper_bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox`
- `C:\repositories\CanDoItAll\tests`

## Deliverables

- Clean targeted builds/tests recorded in execution report.
- Browser screenshots and analytics rows for `/groups/mermaid`.
- Raw note closure table updated with `Solved`, `Partially solved`, or `Not solved`.
- Root README validation summary synchronized.
- Final `validate_bundle.py --stage completed` run recorded.

## Dependency Impact

- This is the final closure gate; weak proof here reopens the relevant earlier subbundle.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted component package build.
2. Run targeted sandbox build.
3. Run targeted component tests.
4. Run targeted MCP tests.
5. Start or reuse sandbox dev server and capture Playwright proof for `/groups/mermaid`.
6. Review screenshots against required visual questions.
7. Update execution report commands, browser analytics, gate results, and raw note closure.
8. Update root README validation summary and subbundle statuses.
9. Run bundle completed validator and repair any failures.

## Scope Exceptions

- None planned. Any partial raw note requires a concrete follow-up item before closure.

## Do Not Do

- Do not mark closure complete with missing browser proof for UI behavior.
- Do not bury missing syntax/MCP coverage in residual risk.
- Do not weaken raw notes that use "must" language.

## Acceptance Checklist

- All targeted builds/tests pass or documented blocker exists with reopened subbundle.
- Browser analytics rows include route, viewport, actions, screenshot paths, and result.
- Raw note closure has no pending rows.
- Final validator passes.

## Proof Required

- `dotnet build src/CanDoItAll.Components.Mermaid/CanDoItAll.Components.Mermaid.csproj`
- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter Mermaid`
- `dotnet test tests/CanDoItAll.Mcp.Mermaid.Tests/CanDoItAll.Mcp.Mermaid.Tests.csproj`
- Playwright screenshots and assertions for `/groups/mermaid`.
- `python C:\Users\dell\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed mermaid_wrapper_bundle`

## Browser Validation Logging

- Route: `/groups/mermaid`
- Viewports: use the evidence captured in subbundle 02 and rerun if stale.
- Required review questions: readability, overlap/clipping, alignment, use of space, controls visible, callback/error panels legible, consistency with sandbox.

## Progression Gate

- Bundle can close only when validators, proof artifacts, gate rows, browser analytics, and raw note closure all agree.

## Suggested Agent Prompt

```text
Execute final validation and closure for the Mermaid wrapper bundle. Run the required builds/tests, capture or verify browser proof for /groups/mermaid, update all execution report rows and raw note closure, then run the completed bundle validator.
```
