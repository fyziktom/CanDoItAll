# Production accepted-use emitter and assimilation wiring

## Status

- Status: `Completed`

## Objective

Implement production-backed accepted-use signal emission and wire professor assimilation scanning into real lifecycle flows.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and SB02 failing-first corpus proves the gap.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs

## Deliverables

- Create a production `ICognitiveMemoryProfessorAcceptedUseSignalEmitter` or equivalent service.
- Emit `ProfessorAnchorAcceptedUse` only after a derived professor memory is used in a recall/workflow result that is accepted or confirmed.
- Prevent mere retrieval, context selection, or unresolved review from counting as accepted use.
- Wire `ScanAssimilationAsync` into scheduled automation or a dedicated lifecycle runner so assimilation is not only manual/test-triggered.
- Add audit metadata linking accepted-use signals to recall trace, synthesized statement, workflow outcome, and derived memory.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Define accepted-use input contract with project id, actor id, recall trace id, synthesis/statement id, derived memory id, and accepted outcome id.
- Reject emission when the referenced memory is the direct professor capture memory, not a derived memory.
- Add DI registration and tests for production emission path.
- Update scheduled automation to run assimilation scans for projects with active professor anchors after consolidation or after accepted-use emission.

## Do Not Do

- Do not count a selected memory as accepted use before an accepted outcome is recorded.
- Do not make tests pass by seeding `ProfessorAnchorAcceptedUse` directly.
- Do not trigger assimilation only from unit-test helper code.

## Acceptance Checklist

- Accepted-use is emitted by production service after accepted outcome.
- Mere recall/retrieval produces no accepted-use signal.
- Automatic scan assimilates/fades an anchor only after independent support, accepted use, and integration proof.

## Proof Required

- `bundle://proof/SB03/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB03/semantic-invariants.md` or `.json`.
- `bundle://proof/SB03/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB03/transcripts/passing.txt`.
- `bundle://proof/SB03/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB03/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariants: `bundle://proof/SB03/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB03/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Production accepted-use emitter and assimilation wiring. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
