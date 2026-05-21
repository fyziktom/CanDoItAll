# Deep dream synthesis and claim-specific provenance

## Status

- Status: `Completed`

## Objective

Replace dream meta-text and coarse record-wide source maps with useful claim-aware synthesis and claim-specific provenance.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and SB02 failing-first corpus proves the gap.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Replace `Conclusion: X is supported by N source-backed observation(s)` final memory text with domain-useful synthesized knowledge.
- Build claim-specific source maps instead of assigning every record source map to every claim unit.
- Make claim grouping include predicate/object/scope enough to avoid over-grouping procedure/policy claims.
- Strengthen entailment validation against negation, numbers, roles, scope, modality, and temporal order.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Split synthesis into claim alignment, contradiction separation, abstraction, caveat generation, and final text realization.
- Keep evidence meta-statements in diagnostics, not in shipped aggregate memory summaries.
- Attach source maps to the specific claim/support fragment that generated each aggregate claim.
- Add tests where one record has two claims with different evidence and prove references do not cross-contaminate.

## Do Not Do

- Do not store evidence-count statements as knowledge.
- Do not join original claim texts as the final aggregate summary.
- Do not assign all source links of a memory to all extracted claims.

## Acceptance Checklist

- Aggregate summaries are useful to an agent without exposing internal scores by default.
- Per-claim provenance resolves to the correct original evidence only.
- Contradictory or weakly supported synthesis goes to review/rejected instead of confident apply.

## Proof Required

- `bundle://proof/SB06/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB06/semantic-invariants.md` or `.json`.
- `bundle://proof/SB06/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB06/transcripts/passing.txt`.
- `bundle://proof/SB06/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB06/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB06/manifest.md`
- Semantic invariants: `bundle://proof/SB06/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB06/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB06/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Deep dream synthesis and claim-specific provenance. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
