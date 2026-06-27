# Proof restart and browser demo

## Status

- `Completed`

## Objective

- Prove the repaired app builds, restarts on the user's 5032 instance, and the Manager tab remains browser-usable.

## Success Criteria

- Targeted tests pass.
- Web project builds.
- Existing 5032 listener is replaced with the rebuilt app.
- `/processes` returns HTTP 200.
- Browser proof opens Manager chat and confirms voice controls/context are present.

## Covered Inputs

- Final closure proof for R001-R005.

## Prerequisites

- Subbundles 01 and 02 completed with tests passing.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\.artifacts\process-manager-audio-reply-and-run-metrics`

## Deliverables

- Build log summary.
- Restarted PID and health check.
- Browser screenshot/DOM proof.
- Final execution report update.

## Dependency Impact

- This is the closure phase; weak proof means the bundle cannot be considered complete.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted tests.
2. Build the web project.
3. Stop the old 5032 listener and start the rebuilt app on port 5032.
4. Health check `/processes`.
5. Use browser automation to open the Manager chat tab and capture proof.
6. Update execution report and final bundle validation.

## Scope Exceptions

- Real microphone input is not required for closure if component tests prove transcription-to-send-to-speech wiring and browser controls load.

## Do Not Do

- Do not leave extra dev servers running.
- Do not skip restart if build succeeds.

## Acceptance Checklist

- Build passes with zero errors.
- `http://localhost:5032/processes` responds successfully.
- Browser proof artifact exists under `.artifacts/process-manager-audio-reply-and-run-metrics`.

## Proof Required

- `dotnet test` targeted commands.
- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj`.
- Restart command/PID.
- Browser screenshot and DOM assertion notes.

## Browser Validation Logging

- Route: `http://localhost:5032/processes`.
- Viewport: desktop large.
- Actions: navigate, open Manager chat tab, inspect voice controls and manager workspace.
- Screenshot: `.artifacts/process-manager-audio-reply-and-run-metrics/manager-chat-desktop.png`.
- Review questions: are Manager chat controls visible, enabled state coherent, and no obvious layout overlap?

## Progression Gate

- Final closure only after tests, build, restart, health check, and browser proof all pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
