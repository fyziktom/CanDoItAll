# Current State Review

## What Codex did improve

1. **Low-signal primary keys are filtered better than before.** `StrongPrimaryFamilies` now excludes `ProjectScope`, `SourceTopology`, `Temporal`, and `AccessRisk`, and the grouping loop filters through `IsStrongPrimaryKey` before creating clusters.
   - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` lines 17-32 and 82-86.

2. **Cluster scoring now records more metrics.** The planner computes cohesion, source independence, source diversity, supporting-signal score, guard penalty, and composite score.
   - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` lines 400-459.

3. **Curator correction targeting is safer than the earlier broad path.** Explicit targets are supported, a single recall candidate may be inferred, and multiple recall candidates create an ambiguous review path rather than immediately superseding all.
   - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 819-844 and 693-731.

4. **Professor anchor state exists.** The code now has `Active`, `Comparing`, `Assimilated`, `Faded`, and `Rejected` states plus a small service that can mark anchors assimilated or faded.
   - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs` lines 85-92 and `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs` lines 11-78.

5. **Aggregate reference expansion exists.** The reference resolver can expand source maps from generated aggregate memories to original dream source maps.
   - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs`.

## What remains insufficient

### 1. Clustering is still single-key grouping

The planner still creates clusters by `GroupBy(entry => new { entry.Key.Family, entry.Key.Key })` after selecting entries that pass `IsStrongPrimaryKey`. Additional keys are calculated only after that single-key group exists.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` lines 82-120.
- Why this is insufficient: adding a weighted score after single-key grouping is not multi-key clustering. It cannot merge memories that are semantically related but do not share the same primary key, and it cannot reliably split memories that share a generic topic/entity token but should not be clustered.

### 2. Key extraction remains heuristic and brittle

`CreateKeys` uses project id, source system/type, `TopicKey` or title, a small token extractor, keyword-like task intents, month, evidence hashes, relation keys, and access/risk state.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` lines 252-317.
- Why this is insufficient: there is no composite feature vector, no pairwise edge explanation, no negative separation signal, no generic-token suppression beyond a few filters, and no stable cluster identity separate from exact member ids.

### 3. Dreaming still produces template text

The dream candidate text contains lines such as `Synthesized aggregate: ...`, `Cluster quality: ...`, `Shared signals: ...`, and `Conclusions: ...`. The conclusions are first-line fragments from existing records.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` lines 540-587 and 589-617.
- Why this is insufficient: a cognitive memory should internalize useful domain knowledge, not preserve diagnostic metrics as canonical memory text. The current output is a template plus fragments, not a semantic abstraction.

### 4. Validation mostly checks plumbing, not meaning

The dream validator checks missing source maps, counts of source memories, contradictory flags, cluster eligibility, duplicate title, stale/restricted records, policy, and generated-only support.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs` lines 106-237.
- Why this is insufficient: it does not prove that a synthesized claim is actually supported by its exact source maps, does not detect near-duplicate aggregates, does not test mixed-topic aggregate claims, and does not model curator-anchor invalidation.

### 5. Aggregate application is too confident

`CalibrateConfidence` adds an evidence boost with `Math.Clamp(distinctSourceItemCount / 4d, 0, 0.12)`, so any non-zero source count reaches the maximum 0.12 boost. A no-issue candidate usually becomes `StrongAccept` because the threshold is 0.86.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs` lines 112-116 and 265-273.
- Why this is insufficient: shallow validator approval can become an approved active memory with strong belief. Dream output should normally start as probationary or weak accept until repeated independent evidence or curator-validated assimilation proves it.

### 6. Curator/professor mode is not yet real learning with assimilation

The curator service captures a turn, creates a source item/evidence/candidate, and applies it immediately when targeting is not ambiguous. The anchor state is set to `Active`, but there is no automatic comparison or assimilation loop.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 153-228, 660-691, and 747-770.
- The professor anchor service only verifies that the requested derived memory exists and is approved/active or stable.
- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs` lines 25-48.
- The existing unit test passes `capture.AppliedMemoryRecordId` as the derived memory.
- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` lines 330-342.
- Why this is insufficient: the direct professor capture itself cannot be proof that the system has internalized the knowledge. Assimilation must require derived memory, aggregate, or repeated independent use that is not the same raw capture.

### 7. Recall synthesis is still concatenation

The recall synthesis service groups selected sections by normalized title and joins the first useful lines.

- Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` lines 24-57 and 142-179.
- Why this is insufficient: a downstream agent should receive a compact answer-shaped brief, not a title-grouped context dump. Detailed provenance should be attached at phrase/claim level and exposed only on demand.

### 8. The tests are too shallow

Several current tests verify that the shallow implementation exists rather than verifying the required behavior:

- `DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics` asserts that canonical text starts with `Synthesized aggregate:` and contains `source-backed conclusions`.
  - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` lines 215-264.
- `RecallSynthesis_MergesRelatedSelectedMemoriesIntoSingleGroundedStatement` asserts concatenation of two source lines under one title.
  - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` lines 834-897.
- `ProfessorAnchor_AssimilatesAndFadesOnlyAfterDerivedMemoryExists` uses the originally applied curator memory as the derived memory.
  - Evidence: `/mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` lines 309-342.

These tests explain why the previous gates passed while core behavior remained too shallow.
