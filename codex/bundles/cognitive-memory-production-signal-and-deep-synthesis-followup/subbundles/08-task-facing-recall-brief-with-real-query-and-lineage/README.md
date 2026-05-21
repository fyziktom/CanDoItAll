# Task-facing recall brief with real query and lineage

## Status

- Status: `Completed`

## Objective

Make recall synthesis use the real query and generate concise task-facing statements with exact statement-to-claim-to-source lineage.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and SB02 failing-first corpus proves the gap.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallMappingAndTypes.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Extend recall synthesis request/context so the actual user query and recall intent reach the brief composer.
- Generate answer/action/caveat/reference-hint statements based on the user's need, not context title/summary only.
- Persist per-statement, per-fragment source maps without Cartesian source/claim expansion.
- Reference resolver returns exact lineage for a selected statement: statement fragment, aggregate claim id, source memory id, source item/evidence anchor, locator, and safe excerpt/summary.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Add query/intent fields to request contracts or pass through an existing recall query context.
- Refactor statement planning so each fragment carries its own aggregate claim and source refs.
- Update persistence to map only the fragment's claims/sources to the statement.
- Add tests that ask for references for one statement and prove unrelated statement sources are excluded.

## Do Not Do

- Do not expose scores, internal trace details, or all references by default.
- Do not use only context pack title and summary as the synthesis query.
- Do not attach every source ref to every aggregate claim id.

## Acceptance Checklist

- Brief text changes meaningfully when the same selected memories are used for different user queries.
- Reference-on-demand returns precise lineage for the requested statement only.
- Default agent-facing output stays concise and useful.

## Proof Required

- `bundle://proof/SB08/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB08/semantic-invariants.md` or `.json`.
- `bundle://proof/SB08/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB08/transcripts/passing.txt`.
- `bundle://proof/SB08/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB08/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB08/manifest.md`
- Semantic invariants: `bundle://proof/SB08/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB08/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB08/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Task-facing recall brief with real query and lineage. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
