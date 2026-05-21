# Professor comparison review lifecycle

## Status

- Status: `Ready`

## Objective

Close the lifecycle gap where professor anchors can remain in `Comparing` after dream validation produces `NeedsHumanReview`.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and SB02 failing-first corpus proves the gap.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorReviewService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Add explicit comparison review records or service methods to resolve `Comparing` anchors.
- Support transitions: accept aggregate/derived memory, reject comparison and return anchor Active, mark anchor Rejected, or request more evidence.
- Persist transition audit signals for every transition.
- Add tests proving no anchor stays `Comparing` after review resolution.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Identify where dream validation sets anchors to `Comparing`.
- Create review resolution commands with actor/reason/outcome metadata.
- Ensure NeedsHumanReview does not silently strand anchors.
- Add UI/API hooks only if existing review UI needs them; otherwise backend service tests are enough.

## Do Not Do

- Do not auto-accept professor comparisons just to clear the state.
- Do not return anchors to Active without audit reason.
- Do not skip tests for `NeedsHumanReview` paths.

## Acceptance Checklist

- Comparing anchors can be resolved deterministically.
- Every transition writes audit signal rows.
- Review resolution preserves direct anchor hiding rules in normal recall.

## Proof Required

- `bundle://proof/SB04/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB04/semantic-invariants.md` or `.json`.
- `bundle://proof/SB04/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB04/transcripts/passing.txt`.
- `bundle://proof/SB04/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB04/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Professor comparison review lifecycle. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
