# rerun-and-project-structure-closure

## Status

- `Blocked until SB01-SB03 pass`

## Objective

Rerun or repair the Tetris delivery process after hardening and prove full closure through APIs, project-structure writeback, and independent app validation.

## Covered Inputs

- N001, N002, N003, N004.
- Requirements R007, R008.

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- SB03 closure gate passed.
- Local app is running and API access is enabled on `http://localhost:5032`.

## Exact Source References

- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `bundle://evidence/run-0cca729a-detail.json`

## Deliverables

- A fresh API-controlled process run or an explicitly repaired run, with final terminal success.
- Final project-structure evidence/verdict node under `custom:7404d4fd10624f468c2524ba618d747b`.
- Independent validation record for the output app proving static/no-backend Tetris behavior.
- Updated `reviews/01-execution-report.md` with commands, API calls, browser analytics, and raw note closure.

## Dependency Impact

- This is the final closure phase. Failure reopens the owning foundation subbundle based on the reopen triggers.

## Validation Depth

- Final closure / verifier subbundle.
- Requires `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.

## Implementation Steps

1. Confirm SB01-SB03 proof manifests and execution report rows are passed.
2. Start a fresh process run through the public APIs as a human/user would.
3. Monitor launch/provision/execution through APIs until terminal state.
4. If the process creates an app, inspect actual output files and project shape.
5. Build/test/publish or static-host the final output using the intended toolchain.
6. Use Playwright to validate the game behavior and localStorage persistence.
7. Read project structure through API and confirm final verdict/evidence node exists under the target `Main app` node.
8. Record all evidence and close raw notes only if the proof honestly passes.

## Scope Exceptions

- Do not edit the final app manually unless the process repair strategy explicitly calls for repairing generated output as part of the rerun. If manual repair is needed, record it as a process failure and reopen SB02/SB03 unless the user explicitly approves manual salvage.

## Do Not Do

- Do not declare success from agent summaries alone.
- Do not accept a failed process with good local artifacts as final closure.
- Do not leave the generated app server running after validation unless the user asks for it.

## Acceptance Checklist

- [ ] API run terminal status is successful.
- [ ] Required artifact expectations are satisfied.
- [ ] No open escalation, dead-lettered outbox, or missing artifact remains.
- [ ] Project structure contains final writeback node/evidence under `Main app`.
- [ ] Output app is static-hostable/no-backend and playable with keyboard controls.
- [ ] High score persists locally.

## Proof Required

- `bundle://proof/SB04/manifest.md` with API transcripts, changed-file hashes if any, final output source assertions, build/test/static proof, browser artifacts, anti-stub audit, and verifier result.
- `bundle://proof/SB04/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Final API snapshot saved under `bundle://evidence/`.

## Browser Validation Logging

- Route: final app game route.
- Viewports: desktop and, if layout is narrow-sensitive, mobile/narrow.
- Required artifacts: screenshot, snapshot, console, keyboard/localStorage assertion JSON.
- Record analytics in `reviews/01-execution-report.md`.

## Progression Gate

- Bundle closure can proceed only when the final run and final app both pass. A good app with failed writeback, or a successful run with a non-interactive/static-mismatch app, fails the gate.

## Suggested Agent Prompt

```text
Execute only SB04 after SB01-SB03 are passed. Use APIs to launch and monitor the process, then independently inspect the output app and project structure. Close only if the process succeeds and the app satisfies the static/no-backend Tetris requirements with browser proof.
```
