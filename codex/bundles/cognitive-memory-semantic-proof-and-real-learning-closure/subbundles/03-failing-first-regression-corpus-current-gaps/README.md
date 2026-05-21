# Failing-first regression corpus for current gaps

## Status

- Status: `Completed`

## Objective

Create red tests for the exact current gaps before implementing feature changes.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs

## Deliverables

- Failing tests for Czech professor capture with diacritics and without English keywords.
- Failing tests proving lexical-only approximate clustering cannot satisfy embedding-backed paraphrase discovery.
- Failing tests rejecting dream text that contains source-map/source-claim meta wording.
- Failing tests where a record has two claims and unrelated evidence anchors; aggregate claim source maps must not include unrelated evidence.
- Failing tests proving accepted-use is emitted by a real outcome event, not only by direct service calls.
- Failing tests for recall statement lineage resolving exact claim/source support.

## Dependency Impact

- Update downstream subbundles, tests, traceability, and proof artifacts if this subbundle changes contracts or service boundaries.
- Re-run prepared-stage validation if this README, requirements, or phase gates are edited.
- Preserve compatibility with existing persistence unless this subbundle explicitly requires schema changes.

## Validation Depth

- Add failing-first proof before production behavior changes.
- Add focused passing tests for the behavior and affected regression tests.
- Include source assertions that prove production behavior, not only tests.
- Include anti-stub audit and red-team negative cases.
- Use portable `repo://` and `bundle://` references only in proof artifacts.

## Implementation Steps

- Add tests first and capture non-zero failing transcript.
- Do not alter production behavior in SB03 except small test infrastructure needed to compile.
- Name each test after a semantic invariant and include it in proof artifacts.
- Keep tests adversarial: no English hints in Czech cases, no shared aliases in embedding cases, no broad source maps in provenance cases.

## Do Not Do

- Do not make tests pass by relaxing assertions.
- Do not manually seed lifecycle events in positive production-path tests.
- Do not use English keywords in Czech capture tests.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB03/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB03/semantic-invariants.md`.
- Completed: `bundle://proof/SB03/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB03/transcripts/passing.txt`.
- Completed: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB03/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Failing-first regression corpus for current gaps. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.


