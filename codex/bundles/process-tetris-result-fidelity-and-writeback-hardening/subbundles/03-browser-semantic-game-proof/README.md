# browser-semantic-game-proof

## Status

- `Ready`

## Objective

Make browser validation prove the delivered Tetris game actually works. The prior validation passed a page that rendered static content but stayed `Status Loading` and ignored keyboard/localStorage behavior.

## Covered Inputs

- N003, N007.
- Requirements R005, R006.

## Prerequisites

- SB02 must define how the app shape/static mode is validated.
- Read `bundle://evidence/tetris-rerun-independent-snapshot.md`, `bundle://evidence/tetris-rerun-independent-console.txt`, and `bundle://evidence/03-blazor-runtime-evidence-pack.md`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://src/CanDoItAll.Web/Components/App.razor`

## Deliverables

- Browser-proof instructions or runtime validation rules requiring semantic interaction checks for game/app delivery.
- Negative proof that the captured bad app would fail validation.
- Positive proof pattern for a correct Tetris app: status ready/playing, keyboard action changes state, high score persists locally.

## Dependency Impact

- SB04 depends on this. Without semantic browser proof, a final rerun can produce another non-playable app and still look green.

## Validation Depth

- Critical foundation with UI proof.
- Requires Semantic Adequacy Gate proof in `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md`.

## Implementation Steps

1. Identify where browser proof requirements are generated for validation/review steps.
2. Add Tetris/game-delivery proof requirements: non-loading status, keyboard effects, localStorage high score, clean console, screenshot/snapshot.
3. Add validation logic or tests that reject proof containing only screenshot, console-clean, and board cell counts.
4. Add a negative fixture using the captured bad behavior: `Status Loading`, score unchanged, localStorage null.
5. Add positive proof expectations for a corrected app.

## Scope Exceptions

- Do not require exact score increments because Tetris pieces/random timing can vary; require observable state change tied to keyboard input and localStorage persistence.
- Do not require mobile controls in this bundle; mobile app was optional later.

## Do Not Do

- Do not accept `New game` click alone as representative interaction proof.
- Do not accept console-clean as proof of interactivity.
- Do not hard-code only this project id; the proof rule should apply to similar game/app delivery tasks.

## Acceptance Checklist

- [ ] Validation requires semantic browser assertions for game delivery.
- [ ] The captured bad app would fail the new criteria.
- [ ] A corrected app has a clear positive proof path.
- [ ] Browser validation analytics format is ready for SB04.

## Proof Required

- `bundle://proof/SB03/manifest.md` with changed-file hashes, Playwright or test transcripts, source assertions, anti-stub audit, and browser artifacts.
- `bundle://proof/SB03/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- Route: final Tetris game route, expected `/game` or static equivalent.
- Viewports: desktop first, narrower viewport if layout changes are expected.
- Required artifacts: screenshot, accessibility snapshot, console file, keyboard/localStorage assertion output.
- Record the analytics row in `reviews/01-execution-report.md`.

## Progression Gate

- SB04 may proceed only after a proof rule/test would reject the observed `Status Loading` app and accepts a proof shape that includes keyboard/localStorage behavior.

## Suggested Agent Prompt

```text
Implement only SB03. Strengthen browser validation so a rendered but non-interactive Tetris page fails. Add explicit proof requirements for status, keyboard-driven state change, and localStorage high-score persistence.
```
