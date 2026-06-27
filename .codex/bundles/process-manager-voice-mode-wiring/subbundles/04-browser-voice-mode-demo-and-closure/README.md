# Browser voice mode demo and closure

## Status

- `Completed`

## Objective

- Prove the final Manager chat voice mode behavior in a real rendered browser, review screenshots, close raw notes, and run final bundle validation.

## Success Criteria

- `/processes` opens and Manager chat renders a selected voice-enabled manager agent.
- `chat-voice-mode-button`, `chat-voice-record-button`, and `chat-voice-speak-button` are enabled.
- Toggling audio mode updates status without disabling the controls.
- Screenshot review confirms controls are visible, aligned, and unclipped.
- Execution report closes every raw note as Solved, Partially solved, or Not solved with proof links.

## Covered Inputs

- N006.
- Final closure for N001 through N005.
- R005 and final R006 proof.

## Prerequisites

- SB02 closure gate passed.
- SB03 closure gate passed.
- Local app/server can run or an explicit host blocker is recorded.

## Exact Source References

- `repo://src/CanDoItAll.Web/Program.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `repo://src/CanDoItAll.AgentFramework.Components/wwwroot/js/agent-framework-voice.js`
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs`

## Deliverables

- Playwright transcript and screenshots under `bundle://proof/SB04`.
- Final raw-note closure in `bundle://reviews/01-execution-report.md`.
- Final validator transcript.
- `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.

## Dependency Impact

- This is the final closure phase. Weak proof here means the user's "real demos/tests" request remains open.

## Validation Depth

- End-to-end UI closure with browser proof, screenshot review, raw-note audit, anti-stub audit, and completed-stage bundle validation.

## Implementation Steps

1. Build/test the affected projects.
2. Start the web app or reuse the existing dev server.
3. Use Playwright to navigate to `/processes`.
4. Open/select the Manager chat tab.
5. Verify voice controls are enabled for a voice-enabled manager agent.
6. Toggle audio mode and verify the status badge.
7. Capture screenshots and transcripts.
8. Run anti-stub audit.
9. Update execution report, proof manifests, semantic invariants, raw-note closure, and final validator results.

## Scope Exceptions

- If browser microphone permission or external provider credentials block real record/playback, record the environment blocker and cite component/provider tests as functional proof.

## Do Not Do

- Do not seed production-only behavior manually as positive proof unless clearly labeled as a test fixture.
- Do not close raw notes from screenshots alone without DOM assertions and test transcripts.

## Acceptance Checklist

- Large-screen browser proof exists.
- DOM assertions show the Manager chat voice buttons are enabled.
- Screenshot review questions are answered.
- Raw-note closure table is updated with proof paths.
- Completed-stage validator passes or explicit blockers are recorded.

## Proof Required

- `bundle://proof/SB04/transcripts/playwright-manager-chat-voice.txt`
- `bundle://proof/SB04/browser/processes-manager-chat-voice-desktop.png`
- `bundle://proof/SB04/transcripts/final-test-run.txt`
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB04/transcripts/completed-validator.txt`
- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`

## Browser Validation Logging

- Route: `/processes`.
- Viewport: maximized desktop or equivalent large viewport first.
- Actions/assertions: navigate, wait for shell, open Manager chat tab, verify selected manager agent, assert voice buttons are enabled, click voice mode, assert audio status.
- Screenshot: `bundle://proof/SB04/browser/processes-manager-chat-voice-desktop.png`.
- Review questions: controls visible, aligned, enabled, unclipped, and status feedback readable.

## Progression Gate

- Final closure passes only when browser analytics, raw-note closure, proof manifests, semantic invariants, and completed-stage validator agree.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
