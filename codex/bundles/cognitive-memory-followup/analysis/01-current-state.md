# Current State Review

## What Codex Actually Improved

### Bundle and skill process

- `candoitall-bundle-workflow/SKILL.md` now contains an outcome contract, gate discipline, feedback closure audit, and final sync language.
- `candoitall-bundle-execution/references/semantic-adequacy-proof.md` now defines semantic proof fields such as shallow-pass trap, adversarial negative proof, semantic positive proof, and anti-stub audit.
- `validate_bundle.py` now has completed-stage semantic proof checks. It extracts critical SB numbers from `## Critical Subbundles` and requires a `## SBxx Semantic Adequacy Evidence` section for completed critical subbundles.

### Cognitive memory behavior

- `CognitiveMemoryClusterPlanner` moved away from a direct top-level `GroupBy(family,key)` plan and now uses candidate pairs plus connected components.
- Low-signal key families are not used as strong preselection keys.
- Dream aggregate candidates now have aggregate claim records and claim/source map records.
- `CognitiveMemoryDreamValidator` checks for missing source maps, weak source independence, contradictory clusters, duplicate aggregates, restricted/redacted content, stale/superseded sources, and active professor anchors.
- `CognitiveMemoryAggregateConfidenceCalibrator` prevents ordinary dream aggregates from always becoming `StrongAccept`.
- `CognitiveMemoryProfessorAnchorService` blocks direct self-assimilation and supports `Assimilated`/`Faded` state transitions.
- Curator correction targeting routes ambiguous multi-target captures to review.
- Recall synthesis hides references by default, and the resolver can expand aggregate/professor lineage.

## Why This Is Still Not Complete

### The workflow skill still cannot force semantic correctness

The workflow skill is improved, but it is still mostly natural-language policy. The executable validator checks whether proof labels exist and whether values look non-empty. It does not verify that referenced commands actually ran, that transcripts exist, that failing-first tests failed before implementation, that source files contain the asserted behavior, or that a semantic negative corpus exists.

Concrete evidence:

- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-workflow/SKILL.md lines 20-44 define a good outcome contract, but no artifact manifest is required before continuation.
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md lines 5-16 list proof labels, but do not require command transcripts, changed-file hashes, red-team verifier output, or failure-before-fix evidence artifacts.
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py lines 612-647 validate proof text by labels and weak-string checks only.
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py lines 650-684 only checks completed critical subbundles that have report sections; it does not inspect the claimed files/tests.
- The prior execution report claims all SB01-SB09 solved, but those claims remain self-reported in C:/repositories/CanDoItAll/codex/bundles/cognitive-memory-execution-depth-professor-learning-followup/reviews/01-execution-report.md.

### Clustering is still exact-key and bridge-prone

`CognitiveMemoryClusterPlanner` is no longer a pure single-key planner, but it still forms candidate pairs only when two records share an exact strong key. Shared content tokens are used only after a pair already exists.

Concrete evidence:

- Candidate pairs are built from exact `Family:Key` groups in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs lines 399-438.
- Candidate preselection accepts only strong exact keys in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs lines 441-444.
- The semantic topic key is normalized from the first non-empty `TopicKey`/title in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs lines 280-283.
- Entity tokens use a basic token splitter and a small English stop-word list in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs lines 125-172.
- Connected components are formed by unioning every accepted pair in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs lines 350-396, but there is no post-component min-cut or internal cohesion split.
- Shared cluster keys require only two members to share a key in large clusters in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs lines 557-567.

Remaining risks:

- Paraphrases and synonyms still miss each other when they do not share exact topic/entity/intent/evidence/relation keys.
- A-B and B-C links can pull unrelated A and C into the same component.
- High-fanout meaningful keys are silently skipped by `MaxCandidateKeyFanout` instead of being handled through bounded sampling plus secondary rare-key evidence.
- Contradiction-only relation clustering can still be underweighted when no additional positive signal exists.

### Dreaming is claim extraction, not true synthesis

Dreaming now has better persistence and source maps, but the generated aggregate text is still made by grouping claim text signatures and selecting a representative source text.

Concrete evidence:

- Claim units are built from existing support claims or safe summaries in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs lines 583-615.
- Claim groups are keyed by token signatures in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs lines 617-628.
- `SynthesizeClaimGroupText` chooses the longest/lexical representative claim, not an integrated synthesis, in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs lines 630-639.
- Aggregate canonical text appends those claim texts in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs lines 548-580.
- The validator's claim-support check is token overlap in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs lines 339-362.

Remaining risks:

- Complementary facts are not integrated into a new statement.
- Claim-level maps can point to broad memory source maps rather than statement-specific evidence.
- Unsupported claims with overlapping generic tokens can pass.
- Valid paraphrased support can fail.
- Dream modes such as `ProcedureMining` and `FailureLearning` are not structurally distinct enough; they mostly change claim kind or selection reason.

### Professor/curator learning is still too direct and keyword-driven

The professor mode is a useful foundation, but it is not yet the comfortable learning loop the user described.

Concrete evidence:

- Capture kind resolution depends on explicit capture kind or keyword heuristics in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs lines 773-800.
- `CreateTrustedImprovementAsync` uses `turn.UserMessage` as the captured correction/summary and immediately creates a trusted consolidation candidate in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs lines 495-770.
- Capture kinds are only `NewKnowledge`, `Correction`, and `WrongScope` in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs lines 64-69.
- Professor anchor state stores only broad state and applied/assimilated memory IDs in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs lines 210-273.
- Assimilation is a manual call in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs lines 11-73 and fading is a manual call in lines 75-102.

Remaining risks:

- Natural professor explanations without words such as `remember`, `wrong`, or `learn this` are not captured.
- Multi-turn professor guidance is not summarized into structured claims, target scope, misconceptions, and correction confidence.
- Direct capture is immediately active memory; it is not clearly separated as a temporary professor anchor excluded from ordinary stable recall.
- Assimilation does not require repeated successful use, independent non-descendant support, or cluster/dream integration proof.
- Fading changes only the capture state; the direct quote memory is not necessarily demoted/retired when the system has internalized the knowledge.
- Correction can still supersede whole memory records instead of claim-level correction when explicit claim targeting is available.

### Recall synthesis is still joining fragments

Recall synthesis is better than title grouping, but it is still deterministic fragment joining.

Concrete evidence:

- Selected memory sections are grouped by query overlap/content key in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs lines 45-76.
- `ComposeStatementText` joins up to four normalized fragments in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs lines 177-203.
- Query-overlap grouping can collapse unrelated selected sections under the same query terms in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs lines 205-226.
- Reference expansion for aggregate memories expands all candidate source maps, not only the claim maps that support the requested synthesized statement, in C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs lines 46-97.

Remaining risks:

- The brief can still be a compressed context dump.
- Conflicts are not separated into clear `known`, `disputed`, and `needs review` statements.
- There is no persisted statement-to-aggregate-claim map, so reference-on-demand cannot precisely explain every sentence.
- Scores/internal references are hidden, but the information is not yet optimally shaped for the specific requester.

### Refactoring was not deep enough

Only the aggregate confidence policy was extracted. Large services remain monolithic, making future Codex simplification likely.

Approximate current sizes:

- `CognitiveMemoryClusterPlanner.cs`: 936 lines.
- `CognitiveMemoryDreamConsolidationService.cs`: 758 lines.
- `CognitiveMemoryCuratorConversationService.cs`: 1141 lines.
- `CognitiveMemoryDreamValidator.cs`: 409 lines.

## Root Cause Of Codex Under-implementation

1. The previous bundle allowed successful closure with self-reported semantic evidence.
2. The validator checked text structure, not source/test artifacts.
3. Tests were often added to match the implementation path rather than adversarially falsify the shallow path.
4. The broad codebase made Codex optimize for small local deltas and passing counts.
5. The bundle did not force a separate red-team verifier after implementation and before closure.
