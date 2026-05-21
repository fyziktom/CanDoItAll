# Natural multilingual professor capture

## Status

- Status: `Completed`

## Objective

Improve curator/professor capture so natural Czech and Q&A teaching are captured as structured professor anchors.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and SB02 failing-first corpus proves the gap.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Add diacritic-insensitive normalization and Czech teaching/correction/scope signals.
- Capture natural Q&A patterns where the user acts as professor without explicit `remember`/`learn this` keywords.
- Extract structured claims, examples, counterexamples, misconception, scope, and confidence.
- Avoid capturing casual conversation or unsupported speculation as professor truth.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Add a normalizer that can compare `špatný rozsah` with `spatny rozsah` while preserving original text for stored memories.
- Add Czech lead-ins such as `proč`, `jak`, `kdy`, `může`, `měla`, and correction phrases with diacritics.
- Add examples/counterexamples extraction tests in both English and Czech.
- Calibrate confidence so ambiguous teaching goes to review rather than direct application.

## Do Not Do

- Do not lower-case and strip diacritics in stored user/professor text.
- Do not require explicit `zapamatuj si` for professor anchors.
- Do not capture every correction as global truth without scope.

## Acceptance Checklist

- Czech diacritics professor messages create anchors.
- Natural Q&A teaching creates anchors with structured claims.
- Ambiguous or unscopeable teaching is reviewed, not silently applied.

## Proof Required

- `bundle://proof/SB05/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB05/semantic-invariants.md` or `.json`.
- `bundle://proof/SB05/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB05/transcripts/passing.txt`.
- `bundle://proof/SB05/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB05/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB05/manifest.md`
- Semantic invariants: `bundle://proof/SB05/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB05/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB05/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Natural multilingual professor capture. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
