# SB07: Final App Validation And Project-Structure Proof

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- Validate the app after agents finish.
- Capture independent browser proof.
- Classify any failure root cause.

## Objective

Prove whether the agent-built app actually works and whether the process recorded screenshots/evidence back into project structure.

## Covered Inputs

- Follow-up request `03-live-blazor-delivery-request`
- `R013`
- `R014`

## Prerequisites

- SB06 live run completed or blocked with final output candidate.
- Output under `C:\programovani\dotnet-demo\output\<run-folder>`.

## Exact Source References

- `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`

## Dependency Impact

- Proof files and project-structure result records.
- No generated product file edits by Codex.

## Validation Depth

- Independent browser screenshot and console capture.
- Output-root file/reference inspection.
- Project-structure evidence readback.
- Failure classification when needed.

## Contract

- Serve or run the final output exactly as agents delivered it.
- Capture independent browser screenshots and console output.
- Verify visible UI behavior, runtime errors, entrypoint consistency, and generated-file references.
- Verify project-structure result nodes/assets contain the agent-run evidence.
- If the app fails, classify the reason as missing skill, missing permission/tool, bad staffing, weak process design, or runtime automation defect.

## Implementation Steps

- Locate final output and agent evidence from run artifacts/project structure.
- Run the delivered app exactly as handed off.
- Use browser automation to capture screenshot and console output.
- Compare independent proof to process-recorded proof.
- Record final verdict and failure classification.

## Do Not Do

- Do not fix the demo app manually.
- Do not edit generated product files.
- Do not accept chat-only or stale screenshot proof.

## Acceptance Checklist

- [x] Proof contains screenshot paths, console transcript, runtime command, output root, and final verdict.
- [x] Project structure contains result/evidence records.
- [x] If rejected, the failure classification points to a concrete process/template/agent/runtime gap.

## Proof Required

- `bundle://proof/SB07/manifest.md`
- `bundle://proof/SB07/screenshots/**`
- `bundle://proof/SB07/transcripts/browser-validation.txt`

## Browser Validation Logging

- Required. Log URL, viewport, screenshot path, console errors/warnings, and visible behavior assertions.

## Progression Gate

- SB07 passes only when the app works with browser proof or fails with a concrete classified root cause.

## Suggested Agent Prompt

Use `bundle://shared-prompts/qa-prompt.md`.
