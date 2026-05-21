# Embedding-backed approximate cluster discovery

## Status

- Status: `Completed`

## Objective

Make approximate clustering robust to paraphrases and missing exact keys by using semantic providers when available.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- SB01 completed and SB02 failing-first corpus proves the gap.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add an approximate candidate provider that can use embeddings/ranker through existing cognitive-memory semantic abstractions.
- Use deterministic lexical fallback when semantic provider is unavailable.
- Normalize diacritics for key comparison while preserving original text.
- Make pair-budget handling deterministic and resumable instead of silently skipping later records.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Add tests with paraphrased records that share no exact strong keys but are semantically close.
- Add tests for unrelated records with overlapping generic words that must not overmerge.
- Record candidate discovery diagnostics: exact pairs, semantic pairs, skipped-budget pages, and continuation cursor.
- Keep cross-project policy constraints intact.

## Do Not Do

- Do not rely only on alias dictionary token overlap.
- Do not compare all pairs unboundedly.
- Do not let pair budget hide missed comparisons without diagnostics/continuation.

## Acceptance Checklist

- Semantic paraphrase candidates are compared and can cluster.
- Generic overlap does not overmerge unrelated memories.
- Budget exhaustion creates continuation/diagnostic proof, not silent loss.

## Proof Required

- `bundle://proof/SB07/manifest.md` with changed-file SHA-256 hashes.
- `bundle://proof/SB07/semantic-invariants.md` or `.json`.
- `bundle://proof/SB07/transcripts/failing-first.txt` unless SB01 process-only exemption is explicitly valid.
- `bundle://proof/SB07/transcripts/passing.txt`.
- `bundle://proof/SB07/transcripts/source-assertions.txt` with producer, consumer, and lifecycle assertions when applicable.
- `bundle://proof/SB07/transcripts/anti-stub.txt`.

## Completion Proof

- Proof manifest: `bundle://proof/SB07/manifest.md`
- Semantic invariants: `bundle://proof/SB07/semantic-invariants.md`
- Passing transcript: `bundle://proof/SB07/transcripts/passing.txt`
- Source assertions: `bundle://proof/SB07/transcripts/source-assertions.txt`

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Do not proceed to dependent subbundles until this subbundle has passing targeted tests and artifact-backed proof.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Embedding-backed approximate cluster discovery. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
