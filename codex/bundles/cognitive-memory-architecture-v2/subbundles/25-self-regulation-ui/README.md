# 25 Self-Regulation UI

## Status

- Ready after `19-metamemory-abstention-calibration`, `24-professor-review-escalation`, `13-interactive-memory-probing-workbench`, and `12-epistemic-drive-engine` where learning evidence is shown.
- Implementation not started.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.

## Objective

Expose self-regulation decisions to operators and users where they affect answers, probing, review, calibration, professor review, and learning decisions.

The UI should make uncertainty actionable without turning the application into a verbose explanation page.

## Covered Inputs

- `inputs/07-cognitive-self-regulation-patch-reference.md`.
- FR-058, FR-059, FR-060, FR-061, NFR-037, NFR-038, and NFR-040.
- Existing UI requirements for trace visibility, review queue, Dialogue Workbench, and Night Reflection/Learning proposal surfaces.

## Prerequisites

- Assessment/posture records exist.
- Answer gate consumes and persists assessment/posture decisions.
- Professor review request/result records and governed action conversion exist.
- Calibration health aggregates exist.
- Dialogue Workbench and review/learning surfaces exist enough to display status and next actions.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\11-ui-and-operator-experience.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\15-interactive-memory-probing.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\24-metamemory-confidence-and-abstention.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\27-cognitive-self-regulation-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\29-calibration-health-and-probing-training.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\30-professor-review-and-escalation.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\self-regulation-test-matrix.md

## Deliverables

- Trace UI additions for self-regulation state, answer posture, warnings, humility triggers, confidence reinforcement, and required operations.
- Calibration health operator surface showing domain/task/model/risk/feature aggregates, bins, drift, overconfidence, underconfidence, wrong-scope, source-insufficient, and abstention quality.
- Professor review detail surface showing review mode, critique, missing evidence, recommended posture, model/profile trace, output hash, and governed resulting actions.
- Dialogue Workbench and answer-rendering surfaces that show answer-gate/self-regulation warnings without styling abstention as a normal answer.
- Browser validation evidence for dense desktop and narrower responsive views.

## Dependency Impact

- Operators can diagnose why the system answered, caveated, asked, audited, probed, reviewed, escalated, or abstained.
- QA can inspect calibration drift, failure patterns, and professor-review governance without reading raw database rows.
- Epistemic Drive and review workflows can expose self-regulation evidence to approval decisions.

## Validation Depth

- Component/page tests for trace rendering and empty/loading/error states.
- Browser tests for Dialogue Workbench answer posture, warnings, blocked/abstained state, calibration health, and professor review detail.
- Access/redaction tests proving restricted evidence is not displayed.
- Visual checks for dense data layout, readable warnings, source limitations, required next actions, and no overlap at desktop and narrower widths.
- Regression tests proving display confidence is labeled as display/projection data and not the decision model.

## Implementation Steps

1. Add view models and query surfaces for assessment/posture, calibration aggregate, and professor review detail.
2. Extend existing trace/review/workbench surfaces using existing CanDoItAll component patterns and Radzen where the project uses it.
3. Add warning/abstention presentation rules that visually distinguish non-answer, caveated answer, source-audit request, probe, review, and professor-review-required states.
4. Add calibration health and professor review views/actions.
5. Add Playwright/browser validation and screenshot artifacts.
6. Update execution report/workbook proof paths.

## Scope Exceptions

- Do not implement new core self-regulation decisions in UI code.
- Do not expose restricted evidence or raw professor prompts beyond policy.
- Do not build a marketing/explanatory page; use operational surfaces.

## Do Not Do

- Do not use custom div-only UI when existing component wrappers exist.
- Do not present display confidence as the decision model.
- Do not hide source-poor, redaction-limited, or professor-review-required warnings.
- Do not style abstention or review-required output as a normal answer.

## Acceptance Checklist

- UI shows self-regulation state, posture, warnings, triggers, score trace summary, and required next actions where answer/probe/review flows expose them.
- Calibration health is inspectable by domain/task/model/risk/feature pattern.
- Professor review detail shows critique, missing evidence, recommended posture, trace metadata, and governed resulting actions.
- Restricted/redacted evidence remains hidden.
- Browser proof covers desktop and narrower layouts.

## Proof Required

- Build/test output.
- Component/page test output.
- Playwright/browser evidence with route, viewport, actions, assertions, screenshots, and result.
- Screenshots for Dialogue Workbench posture/warning, calibration health, and professor review detail.
- Execution report and workbook updates with proof paths.

## Browser Validation Logging

- Required.
- Record route, viewport, Playwright actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Required visual checks: warnings are readable, posture is distinguishable, source sufficiency is visible, blocked/abstained state is not styled as normal answer, calibration tables remain scannable, professor-review actions are clear, and dense viewport remains usable.

## Progression Gate

- Do not proceed to self-regulation closure until UI proof shows posture/warnings/calibration/professor-review evidence is visible, readable, access-safe, and not misleading.
- Reopen this subbundle if a UI path hides self-regulation warnings, leaks restricted evidence, or presents display confidence as the behavior decision.

## Suggested Agent Prompt

Implement self-regulation operator surfaces using existing CanDoItAll UI conventions. Show posture, warnings, triggers, calibration health, professor review governance, and required next actions clearly. Prove with browser evidence that uncertainty and abstention are visible and not rendered as normal answers.
