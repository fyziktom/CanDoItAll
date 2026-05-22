# SB02 Generic Runtime Proof Gates

## Status

- `Completed`

## Objective

Prevent QA and release-readiness acceptance when browser proof is missing, detached, invalid, console-unclear, or too shallow for the process step contract.

## Covered Inputs

- `N001`: "final app was not properly tested"
- `N003`: "items in tetris are not comming ... not visible"
- `N004`: "there is some js trouble in console output"
- `N005`: "this should not happen when I run complicated process like this"
- `R003`, `R004`, `R005`, `R012`

## Prerequisites

- `SB01` progression gate must pass.
- Browser evidence must be available as process artifacts or explicit missing-evidence diagnostics.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.BrowserProof.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Validation that required browser evidence files exist, are non-empty, and match expected media/text type.
- Console diagnostic classification for active proof window versus intentional post-stop disconnects.
- Generic representative-interaction proof enforcement for interactive UI, canvas, game, custom-control, and keyboard-first surfaces when project structure or step contract requires it.
- Conformance observations and repair/block outcomes for missing artifacts, invalid artifacts, active console errors, and shallow proof.
- Recovery instructions that direct the agent to capture fresh, process-visible browser proof.

## Dependency Impact

- `SB03` depends on this gate so process definitions can safely require browser proof.
- `SB04` depends on this gate so a clean development DB run cannot repeat the current false acceptance.

## Validation Depth

- `Critical foundation`
- Requires Semantic Adequacy Gate proof and artifact-backed proof manifest.

## Implementation Steps

1. Add failing-first tests where a QA step requires screenshots but only markdown/result-summary references exist.
2. Add failing-first tests where a screenshot exists but the interaction proof is shallow for a surface identified as interactive by project structure or step contract.
3. Add console classification tests:
   - active validation JavaScript error blocks or selects repair;
   - post-stop disconnect after a durable stop boundary is recorded separately;
   - unclassified log cannot be summarized as warning-free.
4. Implement validation using typed proof requirements and the process artifact records from `SB01`.
5. Add conformance observations for missing, detached, invalid, and shallow browser proof.
6. Update recovery directives so retries ask for exact process-visible artifacts and representative interaction assertions.
7. Update `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Scope Exceptions

- Do not define Tetris-specific gameplay acceptance in process core.
- Do not require exhaustive UI testing for every process. The requirement is representative proof adequate to the step contract.

## Do Not Do

- Do not accept "visible state changed" without checking what state the project/step actually required.
- Do not treat any post-stop console disconnect as automatically fatal.
- Do not make active console errors a residual risk after quality acceptance.
- Do not use status/count-only tests as semantic proof.

## Acceptance Checklist

- Original failure shape is rejected or routed to repair.
- Missing screenshot artifact produces a conformance observation.
- Active JavaScript errors block acceptance.
- Post-stop host disconnects are classified separately and cannot be described as "0 console errors" for the whole log.
- Generic interactive proof uses project structure or step-contract hints, not product-specific runtime code.

## Proof Required

- `proof/SB02/manifest.md` with failing-first and passing transcripts for missing evidence, console classification, and shallow interaction proof.
- `proof/SB02/semantic-invariants.md` with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, and production behavior artifact matrix.
- Targeted unit/integration tests.
- Source assertions showing the production validator emits conformance observations and gates step outcomes.

## Browser Validation Logging

- Required analytics row: `SB02`, route `N/A fixture or local test route`, viewport from fixture when applicable, MCP evidence `screenshot/snapshot/console artifacts from SB01`, screenshot result `valid/invalid`, console phase result `active clean/post-stop classified`.
- Review questions:
  - Does the screenshot prove the requested product state, not just page load?
  - Does the interaction assertion come from project structure or step contract?
  - Are console errors tied to active proof or post-stop cleanup?

## Progression Gate

- Passed. `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md` cite missing-screenshot, active-console-error, post-stop-disconnect, and representative-interaction proof.

## Suggested Agent Prompt

```text
Implement SB02 only. Use SB01 process-visible browser artifacts to gate QA/release acceptance. Add failing-first tests for missing screenshots, active console errors, post-stop disconnect classification, and shallow interactive proof. Keep all process-core checks generic and update proof/SB02 before progression.
```
