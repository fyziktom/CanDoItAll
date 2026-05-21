# End-to-end production quality proof

## Status

- Status: `Completed`

## Objective

Prove the entire corrected cognitive memory loop end to end with production pathways and artifact-backed proof.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01-SB09 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Add an end-to-end red-team test for multilingual professor learning through capture, dreaming, review, accepted use, assimilation/fade, recall, and reference-on-demand.
- Run targeted cognitive memory tests and any affected full test suite segment.
- Produce proof manifests for all critical subbundles under `proof/SBxx/`.
- Run completed-stage validator with the strengthened validator.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Construct a scenario where memory has a wrong or incomplete belief and the professor teaches the correction naturally in Czech/English.
- Prove direct professor anchor is not shown by default recall.
- Prove dream comparison creates a reviewable derived memory and review resolves it.
- Prove accepted-use is emitted only after accepted outcome and then assimilation/fading occurs.
- Prove final recall brief is concise and references the exact source lineage on demand.

## Do Not Do

- Do not manually seed accepted-use signals in the final E2E test.
- Do not close with only unit tests for isolated helpers.
- Do not skip completed-stage bundle validation.

## Acceptance Checklist

- Full professor-learning lifecycle passes end to end.
- No critical proof manifest is missing producer/consumer/lifecycle evidence.
- Completed validator passes with strengthened gates and fake-proof fixtures remain failing.

## Proof Required

- `bundle://proof/SB10/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB10/semantic-invariants.md` or `.json`.
- `bundle://proof/SB10/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB10/transcripts/passing.txt`.
- `bundle://proof/SB10/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB10/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB10/manifest.md`
- Semantic invariants: `bundle://proof/SB10/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB10/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB10/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement End-to-end production quality proof. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
