# 03 Validation And E2E

## Status

- `Completed`

## Objective

Prove the implementation with automated tests and a real 5032 project-structure floating agent chat.

## Deliverables

- Focused unit/integration test transcripts.
- Build transcript.
- CodeAnalytics/dependency scan transcript.
- 5032 restart proof.
- Floating chat proof against project `f28c07cd-982c-4d2d-bcf2-3e60a32eca72`.
- Runtime/tool-event observations.

## Covered Inputs

- R008

## Prerequisites

- `01-managedcode-document-converter` passed.
- `02-workspace-tool-wiring` passed.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://src/App/CanDoItAll.Web`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`

## Dependency Impact

- No product-code changes should be made in this subbundle except test/proof repairs required by earlier work.

## Validation Depth

- Full closure.

## Implementation Steps

1. Run focused tests.
2. Build affected projects/app.
3. Run CodeAnalytics/dependency checks.
4. Restart 5032.
5. Use the project-structure floating agent chat to ask for conversion/extraction from the quotation PDF.
6. Inspect tool/run logs for `workspace_convert_document` success, duration, and failure causes.

## Do Not Do

- Do not hide project-structure write denial as a conversion success.
- Do not mutate agent permissions in this validation phase unless explicitly included in the bundle scope.

## Acceptance Checklist

- The agent-visible conversion tool no longer fails with missing Python MarkItDown.
- The quotation PDF can be converted to markdown through the live agent tool path.
- Any project-structure node-write failure is clearly identified as access policy, not document conversion.

## Proof Required

- Test/build transcripts.
- Browser snapshot or screenshot proof.
- Agent run/tool-event summary.

## Browser Validation Logging

- Route: `localhost:5032` project `f28c07cd-982c-4d2d-bcf2-3e60a32eca72` structure view.
- Viewport: large desktop.
- Required action: open floating agent chat, send prompt referencing quotation PDF asset, observe conversion/extraction result.

## Progression Gate

- Bundle may close only after live conversion is proven or a hard external blocker is recorded.

## Validation Result

- Live conversion was proven through the 5032 project-structure floating chat.
- The agent read the PDF asset, called `workspace_convert_document`, and extracted `ZM-x5600` with `$35,000 USD`.
- Project-structure node creation remained blocked after UI approval because the approval continuation flow did not resume execution.

## Suggested Agent Prompt

```text
Validate the completed document conversion tool through the running 5032 app and project-structure floating agent chat. Record tool access, timing, and any permission-related limitation separately from conversion behavior.
```
