# SB04 Regression And Demo-Readiness Proof

## Status

- `Ready`

## Objective

Prove the repaired generic process cannot repeat the current DB failure and prepare a clean development DB so the user can rerun the whole workflow and multi-team software-delivery flow.

## Covered Inputs

- `N001`: "final app was not properly tested"
- `N002`: "there are not screenshots evidences"
- `N003`: "Playwright MCP ... would discover..."
- `N004`: "js trouble in console output"
- `N005`: "this should not happen when I run complicated process like this"
- `R009`, `R010`, `R011`

## Prerequisites

- `SB01`, `SB02`, and `SB03` closure gates must pass.
- Development DB reset/cleanup commands must be identified and reviewed before use.
- If the repo already has DB setup scripts, use those rather than ad hoc destructive SQL.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `repo://codex/bundles/process-browser-evidence-runtime-proof-hardening/reviews/01-execution-report.md`
- `bundle://inputs/01-source-artifacts.md`

## Deliverables

- Regression tests for the exact DB failure shape.
- Clean development DB setup steps captured as command proof.
- Fresh workflow and multi-team software-delivery run with browser proof.
- Process artifact records for screenshot, console, snapshot/DOM, and interaction evidence.
- Browser validation analytics and screenshot review in the execution report.
- Final closure review showing the original raw notes are closed or explicitly deferred.

## Dependency Impact

- This is the final closure phase. If it fails, reopen the earliest subbundle whose invariant broke.

## Validation Depth

- `Process-critical closure`
- Requires final red-team/fake-proof resistance review.

## Implementation Steps

1. Add or run regression tests proving the original DB failure shape is rejected.
2. Run targeted tests from `SB01`, `SB02`, and `SB03`.
3. Identify the sanctioned clean-development-DB setup path. Record exact commands and before/after DB identity.
4. Prepare the clean development DB only after code-level tests pass.
5. Rerun the workflow and multi-team software-delivery process.
6. Use Playwright MCP browser proof for the UI/runtime step:
   - navigate to the actual URL;
   - perform representative interaction from project structure hints;
   - capture screenshot;
   - capture snapshot/DOM or evaluate output;
   - capture console diagnostics before stop and after cleanup classification;
   - verify process artifact records include the evidence.
7. Fill the execution report and final closure rows.

## Scope Exceptions

- The final generated app may still have product bugs. If the generic process catches them and routes repair/block correctly, the process hardening succeeds. Product bugs should become visible outcomes, not hidden successes.

## Do Not Do

- Do not manually insert process artifact records as proof.
- Do not declare success from raw `.playwright-mcp` paths alone.
- Do not reset or delete unrelated user data without using the repo's sanctioned development DB setup path.
- Do not skip screenshot review because the automated test passed.

## Acceptance Checklist

- Original failure fixture fails before the fix and passes after the generic repair.
- Fresh process run records screenshot, console, and snapshot/DOM evidence as process artifacts.
- Browser analytics include route, viewport, actions, screenshot path, console path, assertions, and review result.
- Active console errors route to repair/block; post-stop disconnects are classified.
- Clean development DB is ready for the user to test.

## Proof Required

- Targeted `dotnet test` transcripts.
- Clean DB setup transcript.
- Fresh run id and step ids.
- SQL or API query showing browser evidence artifact records.
- Playwright MCP screenshot/snapshot/console paths under scoped process artifacts.
- Final red-team note proving markdown-only or detached `.playwright-mcp` evidence cannot pass.

## Browser Validation Logging

- Required analytics row: `SB04`, route actual localhost URL, viewport at least one desktop pass, Playwright MCP actions `navigate, representative interaction, snapshot/evaluate, screenshot, console`, screenshots scoped process artifact paths, result `Passed/Failed`.
- Screenshot review questions:
  - What visible product state is proven?
  - What representative behavior was exercised?
  - What project-structure or process-step requirement supplied the expected behavior?
  - Are any UI elements missing, invisible, clipped, or stale?
  - Were console diagnostics clean during active validation?

## Progression Gate

- Final closure passes only when the clean-DB process run produces process-visible browser evidence and the execution report proves the original failure shape can no longer be accepted.
- The execution report must cite `proof/SB04/manifest.md`, `proof/SB04/semantic-invariants.md`, browser analytics, SQL/API artifact-record proof, and final raw-note closure.

## Suggested Agent Prompt

```text
Implement SB04 only after SB01-SB03 gates pass. Run the regression suite, prepare a clean development DB through the repo-approved path, rerun workflow and multi-team software delivery, capture Playwright MCP proof, and verify browser evidence appears in process artifact records. Fill the execution report and stop if any proof is detached or shallow.
```
