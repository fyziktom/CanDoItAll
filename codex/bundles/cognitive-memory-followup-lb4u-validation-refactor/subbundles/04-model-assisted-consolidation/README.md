# 04 Model Assisted Consolidation

## Status

- Status: `Completed`

## Objective

Improve source chunking and consolidation so candidates are meaningful, source-backed, reviewable memories rather than shallow keyword classifications.

## Covered Inputs

- LB4U staged manifest from subbundle 02.
- Model profile/token settings from subbundle 03.
- Current consolidation services and tests.
- Original v2 consolidation/review invariants.

## Prerequisites

- Subbundles 02 and 03 must pass.
- Existing consolidation tests must be understood.
- Review-gated canonical mutation must remain intact.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Ingestion
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi
- C:\repositories\CanDoItAll\tests

## Deliverables

- Improved chunk extraction and candidate generation.
- Candidate types for project facts, procedures, decisions, risks, assumptions, and reusable knowledge where existing taxonomy supports them.
- Tests proving LB4U-like inputs generate useful source-backed candidates.
- Review-item creation remains required for canonical changes.

## Dependency Impact

- Feeds epistemic drive, probing, OpenAI validation, and refactor subbundles.
- May require schema or contract changes; if so, update tests and migration expectations.
- Must coordinate with subbundle 07 before moving logic into helper files.

## Validation Depth

- Unit tests for extraction/chunking and candidate quality.
- Integration tests for consolidation run persistence.
- Negative tests for unsupported or ungrounded generated candidates.
- Token/truncation checks through subbundle 03 metadata.

## Implementation Steps

1. Add failing tests using LB4U-shaped source content.
2. Introduce typed chunk/source-span helpers if needed.
3. Add model-assisted candidate generation under explicit model profile control.
4. Keep deterministic policy checks around model output.
5. Ensure candidates carry raw source references.
6. Verify review requirements before applying canonical memory.

## Do Not Do

- Do not write model output directly to canonical truth.
- Do not create generic planning knowledge without support.
- Do not hide model failures behind deterministic summaries.
- Do not break existing consolidation API contracts without documenting them.

## Acceptance Checklist

- Candidates include meaningful source-backed content.
- Candidate source spans are traceable.
- Review gate remains mandatory.
- Existing and new tests pass.
- Weak or unsupported output is rejected or marked for review.

## Proof Required

- Test output.
- Sample generated candidate records with source references.
- Consolidation run evidence.
- Workbook update.

## Execution Proof

- Added Office/PDF external-source text extraction for `.docx`, `.pptx`, `.xlsx`, `.pdf`, and text-like files.
- Added `CognitiveMemoryConsolidationFactExtractor` for source-backed planning dimensions and localized Czech/Slovak business terms.
- Added contact-heavy/PII-heavy consolidation skip behavior and bumped consolidation algorithm version to `consolidation-v3`.
- Live LB4U refresh processed 43 source items, created 39 useful candidates after the contact filter, and removed noisy pending review items by rejection.

## Browser Validation Logging

- Browser validation is required only if review/consolidation UI is changed.
- Log route, viewport, evidence, screenshots, and result if UI changes occur.

## Progression Gate

- Proceed to subbundles 05, 06, and 07 only after candidate quality and review gates pass.

## Suggested Agent Prompt

Improve consolidation quality with typed chunks, source spans, and model-assisted candidate generation. Preserve review-gated truth and fail visibly on model/truncation problems.
