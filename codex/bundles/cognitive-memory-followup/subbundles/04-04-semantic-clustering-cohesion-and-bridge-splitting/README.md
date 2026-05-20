# SB04 - Semantic clustering cohesion and bridge splitting

## Status

- Status: `Completed`

## Objective

Finish clustering so it uses composite semantic evidence, handles paraphrases, preserves contradiction clusters, and splits weak bridge components.

## Covered Inputs

- Current pair formation starts from exact shared strong keys.
- Connected components can over-merge bridge chains.
- Contradiction-only relations and high-fanout fallback need stronger handling.

## Prerequisites

- SB03 failing-first clustering tests exist and fail.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Extract key extraction and candidate-pair selection collaborators.
- Add alias/phrase/keyphrase signals and optional semantic-similarity provider interface with deterministic test fake.
- Add bounded fallback pair generation for high-fanout keys using rare secondary signals.
- Add contradiction relation pairs as review candidates even without positive shared tokens.
- Add component cohesion splitting/min-cut or route low-cohesion bridge components to review.

## Dependency Impact

- Unblocks dream synthesis quality in SB05.
- Feeds professor anchor integration in SB06-SB07.
- Feeds recall source quality in SB08.

## Validation Depth

- Targeted clustering tests must fail before and pass after.
- Assert overbroad bridge cluster is split or marked review-only.
- Assert paraphrased memories cluster through semantic/alias evidence, not exact title/topic only.
- Assert contradiction-only relation creates a review cluster.

## Implementation Steps

- Extract `ICognitiveMemoryClusterKeyExtractor` and `ICognitiveMemoryCandidatePairSelector` or equivalent.
- Add deterministic phrase/keyphrase extraction beyond first 10 tokens.
- Add alias map support for domain/professor anchors.
- Add internal cohesion scoring across all member pairs, not only primary key replicas.
- Split or review connected components where endpoint cohesion is below threshold.
- Persist algorithm version update.

## Do Not Do

- Do not just lower thresholds until tests pass.
- Do not form aggregate-ready clusters from project/time/access keys alone.
- Do not hide overbroad bridge clusters by dropping them silently.

## Acceptance Checklist

- Paraphrased related records cluster without exact shared topic/title.
- Bridge overmerge fixture is split or review-only.
- Contradiction relation-only fixture is review cluster, not missed.
- High-fanout key fixture uses bounded fallback or produces warning/review instead of silent discard.
- Cluster quality metrics reflect actual internal member cohesion.

## Proof Required

- `proof/SB04/manifest.md` with failing-first and passing transcripts.
- Targeted clustering test transcript: `proof/SB04/transcripts/passing-targeted-clustering-tests.txt`.
- Source-level assertion for candidate pair formation and bridge splitting: `proof/SB04/transcripts/source-assertions.txt`.
- Anti-stub scan transcript: `proof/SB04/transcripts/anti-stub-audit.txt`.

## Browser Validation Logging

- N/A unless cluster search UI display changes; then run component/UI proof.

## Progression Gate

- SB05 cannot close until SB04 clustering tests pass.
- If clustering still relies only on exact strong-key equality, SB04 remains incomplete.

## Suggested Agent Prompt

Implement SB04. Replace exact-key-only candidate formation and add cohesion-based bridge splitting/review while preserving deterministic tests.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
