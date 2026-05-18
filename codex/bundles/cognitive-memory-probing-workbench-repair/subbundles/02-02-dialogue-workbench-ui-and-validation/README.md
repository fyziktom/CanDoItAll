# 02-dialogue-workbench-ui-and-validation

## Status

- `Completed`

## Objective

- Add a usable Dialogue Workbench to the Cognitive Memory page and validate it with the AI Tap/Faucet and Curacao Glass projects.

## Success Criteria

- User can start or reuse a probe session from `/cognitive-memory?projectId=...`.
- User can ask an arbitrary question and see returned memory context, source refs, warnings, and trace stages.
- User can submit typed feedback with notes/correction text and optional review/regression flags.
- Workbench exposes enough review/repair state that the user can understand what will be fixed.
- Browser proof confirms readable layout and visible controls.

## Covered Inputs

- R-001, R-002, R-003, R-007, R-008.
- Raw notes N002 and N003.

## Prerequisites

- `01-01-probing-feedback-repair-core` closure gate passed.
- API host can run locally.
- Realistic project ids are available in PostgreSQL.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryReviewUiServiceTests.cs`
- `C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\source-manifest.json`

## Deliverables

- Dialogue Workbench UI inside the Cognitive Memory page.
- View models/state for current session, question, answer, sources, trace, and feedback.
- Feedback submit actions wired to `ICognitiveMemoryProbeService`.
- Browser validation artifacts.

## Dependency Impact

- Future Epistemic Drive question queues and calibration dashboards will depend on this workbench as the user-facing probe surface.

## Validation Depth

- Critical UI foundation with browser proof.

## Implementation Steps

1. Inject `ICognitiveMemoryProbeService` into the Cognitive Memory page.
2. Add workbench state and handlers for start, ask, feedback, and refresh.
3. Render answer sections/source refs/trace stages from the returned probe answer.
4. Add feedback controls and correction text area.
5. Add CSS using existing page conventions.
6. Validate with tests, API smoke, and browser proof.

## Scope Exceptions

- Generated question queue is not required in this pass.
- A rich span-selection editor is not required; plain text correction plus typed action is acceptable for MVP.

## Do Not Do

- Do not implement memory logic in Razor components.
- Do not hide warnings or source insufficiency.
- Do not use Radzen components.
- Do not create a marketing-style page; keep this operational and dense.

## Acceptance Checklist

- `Passed` - Workbench can ask about AI Tap organization/facility plan.
- `Passed` - Workbench can ask about Curacao finance/risk through API smoke using the same probe service and realistic project id.
- `Passed` - Feedback submission returns a persisted visible success state: browser proof shows `Saved ba25856a`.
- `Passed` - Correction feedback creates review-gated repair candidates; API proof approved review item `9022d49b-1487-433a-aa0d-8cd9d431a23d` into memory record `0220c9c6-b1e0-4df7-9d4f-a956f4f9d478`.
- `Passed` - `RequestReview` and `CreateRegression` action semantics are honored by backend tests even when the UI checkboxes are not selected.
- `Passed` - Desktop and narrow screenshots show readable controls without overlap after the field-width repair.

## Proof Required

- `Passed` - Targeted tests: `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemoryAdvancedServicesTests|FullyQualifiedName~CognitiveMemoryReviewUiServiceTests" --no-restore -m:1`.
- `Passed` - API smoke against AI Tap `a845e5c9-43b5-4885-b970-7a63474029c3` and Curacao Glass `76770384-d515-40ce-9924-78a4a59b4f86`: `validation/evidence/api-smoke/api-probe-smoke-results.json`.
- `Passed` - Browser proof on `/cognitive-memory?projectId=a845e5c9-43b5-4885-b970-7a63474029c3`.
- `Passed` - Desktop screenshot `validation/evidence/browser/probe-workbench-desktop.png` and narrow screenshot `validation/evidence/browser/probe-workbench-mobile.png`.

## Browser Validation Logging

- Route: `/cognitive-memory?projectId=a845e5c9-43b5-4885-b970-7a63474029c3`.
- Viewports: desktop `1600x950`; narrow `390x844`.
- Actions: navigate, start session, ask question, verify answer sections/source refs, submit feedback dialog/form, screenshot.
- Screenshot review: no overlap, trace/source readable, feedback controls visible, warnings visible.
- Validation note: first browser pass found the question textarea rendered too narrow; fixed by making `.cognitive-memory-field` and its input/select/textarea children consume available width, rebuilt, and recaptured screenshots.

## Progression Gate

- `Passed` - Browser proof and realistic-project API evidence are captured.

## Suggested Agent Prompt

```text
Implement the Dialogue Workbench on the Cognitive Memory page using existing component conventions. It must start a probe session, ask a project-scoped question, render answer context/source/trace evidence, and submit typed feedback against the backend repair path. Validate with the AI Tap/Faucet and Curacao Glass projects and capture browser proof.
```
