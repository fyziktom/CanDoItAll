# Failing-first regression corpus for current gaps

## Status

- Status: `Ready`

## Objective

Create failing-first tests that demonstrate the remaining gaps in the current implementation before any production fixes are made.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and installed.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs

## Deliverables

- Add failing-first tests for accepted-use production emission, automatic assimilation scan wiring, dream meta-text rejection, claim-specific source maps, Czech diacritics professor capture, query-aware recall synthesis, precise reference lineage, semantic paraphrase discovery, and Comparing-anchor review resolution.
- Capture a failing-first transcript before production fixes.
- Name each test in the semantic invariant contract.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Write tests that fail on the current code without changing production behavior.
- Avoid manual insertion of `ProfessorAnchorAcceptedUse` in positive production-emission tests.
- Add at least one Czech test with real diacritics and one natural Q&A professor correction without explicit `remember` phrases.
- Add at least one aggregate test that fails if final text contains `supported by N source-backed observation(s)` as the shipped memory.

## Do Not Do

- Do not update expected outputs to match current shallow behavior.
- Do not seed production-only lifecycle signals directly in the positive tests.
- Do not skip failing-first because a similar older test exists.

## Acceptance Checklist

- The targeted test transcript has a non-zero exit code before production fixes.
- Every later production subbundle cites at least one of these failing-first tests.
- The tests prove behavior, not only row counts.

## Proof Required

- `bundle://proof/SB02/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB02/semantic-invariants.md` or `.json`.
- `bundle://proof/SB02/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB02/transcripts/passing.txt`.
- `bundle://proof/SB02/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB02/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Failing-first regression corpus for current gaps. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
