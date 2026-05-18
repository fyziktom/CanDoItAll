# 05 Epistemic Cross Project Knowledge

## Status

- Status: `Ready`

## Objective

Improve epistemic drive and cross-project promotion so reusable planning knowledge can emerge from source-backed evidence and review decisions without being manually seeded.

## Covered Inputs

- Original epistemic drive and cross-project requirements.
- LB4U business-plan and planning stages.
- Current advanced services.
- Consolidation improvements from subbundle 04.

## Prerequisites

- Subbundle 04 must pass.
- There must be source-backed candidate facts from LB4U stages.
- Review decision behavior must be available.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Foundation
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2

## Deliverables

- Coverage scan dimensions for business plans, marketing, staffing, expenses, procurement, release plans, and technical architecture.
- Gap proposals that cite missing or weakly covered dimensions.
- Cross-project candidate logic that separates LB4U-specific facts from reusable planning knowledge.
- Tests showing unsupported generic rules are rejected or remain unaccepted.

## Dependency Impact

- Feeds OpenAI validation and probing quality.
- Must not require multiple real projects to pass basic candidate behavior, but must remain ready for future multi-project data.
- May share helpers with consolidation and probing.

## Validation Depth

- Unit tests for coverage scoring and proposal creation.
- Tests for source-backed versus unsupported reusable knowledge.
- Review decision tests for accept/reject paths.
- Probe checks after study cycles.

## Implementation Steps

1. Define coverage dimensions and typed scan output.
2. Scan canonical/source coverage for LB4U business-plan stages.
3. Generate learning proposals with source support.
4. Reject or flag generic rules without evidence.
5. Test cross-project promotion candidate state without direct truth mutation.
6. Update workbook validation matrix.

## Do Not Do

- Do not hand-author the desired business-plan knowledge as canonical memory.
- Do not promote single-source speculation as cross-project truth.
- Do not bypass review because a model answer sounds plausible.
- Do not confuse LB4U facts with reusable rules.

## Acceptance Checklist

- Coverage dimensions are explicit.
- Gap proposals include evidence and missing-dimension rationale.
- Reusable knowledge requires source support and review.
- Tests prove unsupported generic rules do not become accepted truth.

## Proof Required

- Test output.
- Example scan/proposal results.
- Review decision evidence.
- Workbook update.

## Browser Validation Logging

- Browser validation is required only if epistemic-drive UI is changed.
- Capture route and evidence if changed.

## Progression Gate

- Proceed to OpenAI validation only after coverage and reusable-knowledge behavior is testable.

## Suggested Agent Prompt

Improve epistemic drive so reusable planning knowledge emerges from source-backed consolidation and review. Do not seed generic rules directly.
