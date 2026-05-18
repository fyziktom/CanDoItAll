# 06 Probing Feedback Regression Loop

## Status

- Status: `Ready`

## Objective

Make probing behave like a human study loop: ask memory questions, inspect source context, give feedback, request deeper study, and verify improvement with regression probes.

## Covered Inputs

- Memory probing script.
- Current probe/session/feedback services.
- Consolidation and epistemic improvements.
- LB4U staged source facts.

## Prerequisites

- Subbundle 04 must pass.
- Subbundle 05 should be complete or ready enough for reusable-knowledge probes.
- Model profile settings from subbundle 03 must be available if answer generation uses models.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-followup-lb4u-validation-refactor\inputs\04-memory-probing-script.md

## Deliverables

- Probe session observation model or equivalent evidence capture.
- Regression probe set for LB4U and planning knowledge.
- Feedback handling that records accepted, rejected, and deeper-study outcomes.
- Tests for before/after improvement and source-backed answers.

## Dependency Impact

- Feeds OpenAI and Ollama validation.
- May require API result shape additions for evidence capture.
- Must coordinate with docs/skill closure.

## Validation Depth

- Unit tests for feedback decisions.
- Integration/API tests for probe session flow.
- Regression probe tests using controlled memory fixtures.
- Manual-style validation during OpenAI/Ollama cycles.

## Implementation Steps

1. Add probe regression fixtures based on LB4U questions.
2. Ensure probe results include context sources and answer-quality metadata.
3. Add feedback paths for approve/reject/deeper study.
4. Add before/after improvement tests.
5. Record evidence in workbook and execution report.

## Do Not Do

- Do not let probe feedback directly mutate canonical truth.
- Do not accept generic model answers without source context.
- Do not skip rejected-answer evidence.
- Do not make tests brittle by requiring exact prose from a model.

## Acceptance Checklist

- Probe answers expose source context.
- Feedback decisions are persisted.
- Deeper-study loop can improve a missed source-backed answer.
- Regression probes distinguish supported from unsupported answers.

## Proof Required

- Test output.
- Probe session ids or fixture proof.
- Before/after answer summaries.
- Workbook update.

## Browser Validation Logging

- Browser validation is required if probe UI is changed.
- Capture route, viewport, screenshots, and result for changed UI.

## Progression Gate

- Proceed to OpenAI validation only after probe regression behavior is testable.

## Suggested Agent Prompt

Implement and test the human probing loop. Answers must cite memory context, feedback must be review-safe, and deeper study must show measurable improvement.
