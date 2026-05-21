# Maintainability boundaries and options cleanup

## Status

- Status: `Completed`

## Objective

Refactor large cognitive memory services into maintainable units without weakening behavior.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB03-SB08 behavior fixes completed and tests passing.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs

## Deliverables

- Split large services into cohesive components with interfaces and DI registrations.
- Move algorithm options to DI/config paths; keep test helpers explicit and avoid hidden singleton fallbacks in production paths.
- Add service composition tests proving all new interfaces resolve.
- Add an inventory of old/new responsibilities and file-size/method-size targets.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Refactor after behavior tests from SB02-SB08 are green.
- Use adapters/facades to preserve public API where possible.
- Keep code comments in English if new comments are added.
- Run full cognitive memory test suite after refactor.

## Do Not Do

- Do not refactor by moving large methods unchanged into new files without improving boundaries.
- Do not add more responsibilities to existing oversized services.
- Do not hide options by creating new static `Current` calls.

## Acceptance Checklist

- Major services have clear subcomponents and reduced responsibility scope.
- DI tests pass for production service graph.
- No behavior tests from earlier subbundles regress.

## Proof Required

- `bundle://proof/SB09/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB09/semantic-invariants.md` or `.json`.
- `bundle://proof/SB09/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB09/transcripts/passing.txt`.
- `bundle://proof/SB09/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB09/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB09/manifest.md`
- Semantic invariants: `bundle://proof/SB09/semantic-invariants.md`
- Responsibility inventory: `bundle://proof/SB09/responsibility-inventory.md`
- Passing transcript: `bundle://proof/SB09/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB09/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Maintainability boundaries and options cleanup. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
