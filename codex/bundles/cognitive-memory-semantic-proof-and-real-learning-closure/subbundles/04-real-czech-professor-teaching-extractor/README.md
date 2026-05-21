# Real Czech and multilingual professor teaching extractor

## Status

- Status: `Completed`

## Objective

Replace English-only keyword capture with a multilingual teaching extraction pipeline suitable for natural professor/student dialogue.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Add language-aware normalization with diacritic folding for matching only, while preserving original captured text.
- Add Czech and English teaching/correction/scope/example/counterexample signals.
- Add structured extraction for claims, target scope, misconception, examples, counterexamples, language, and confidence.
- Add an optional semantic classifier/provider interface for future LLM/ranker extraction, with deterministic fallback tests.
- Refactor extraction out of the large conversation service where needed.

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

- Create normalization helpers and tests for `schválení`/`schvaleni`, `příklad`/`priklad`, `protipříklad`/`protipriklad`, and `mýlíš`/`mylis`.
- Add Czech Q&A teaching cases without explicit `remember`, `learn`, or English words.
- Ensure source utterances preserve original text and diacritics.
- Persist structured anchor metadata needed for later dream comparison and fading.

## Do Not Do

- Do not convert stored original utterances to accent-stripped text.
- Do not claim Czech support with English markers inside Czech test messages.
- Do not require the user to manually label every teaching turn.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB04/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB04/semantic-invariants.md`.
- Completed: `bundle://proof/SB04/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB04/transcripts/passing.txt`.
- Completed: `bundle://proof/SB04/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB04/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Real Czech and multilingual professor teaching extractor. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.


