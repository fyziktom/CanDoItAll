# Current-State Review

## Overall Assessment

The current implementation adds important tables, services, UI surfaces, and tests, but it is still a first-pass quality scaffold. It can demonstrate that records are clustered, dream candidates are created, validations are saved, curator turns are captured, and synthesized recalls can be persisted. It does not yet prove that the memory is deeply organizing knowledge, validating synthesized meaning, or assimilating professor corrections.

## Finding F-01: Clustering is single-key grouping, not weighted multi-key clustering

`CognitiveMemoryClusterPlanner.PlanAsync` creates keys for each record and then groups entries by family/key. The cluster primary family is exactly the single grouping family. This means every enabled key family can independently create a cluster, even if the key is low-signal.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` lines 52-105: `keyEntries.GroupBy(...)` creates clusters from one key at a time.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs` lines 209-233: default `KeyFamilies` is `Enum.GetValues`, so every key family is enabled by default.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` lines 214-275: key generation includes project scope, source topology, month, access/risk, and simple tokens.

Why this is a problem:

- `ProjectScope` can group almost every record in a project.
- `Temporal` can group unrelated records updated in the same month.
- `AccessRisk` can group unrelated records that merely share access/risk state.
- `SourceTopology` currently only uses source system and source item type, which can be too coarse.
- `Entity` extraction is token-based rather than entity-aware.
- The cluster plan has no cohesion score, evidence-independence score, source diversity score, or max-size/split behavior.

Existing tests reinforce this weak behavior. `CognitiveMemoryQualityFoundationTests` expects clusters for all key families, including broad families, rather than proving that low-signal keys only participate as supporting features.

## Finding F-02: Dreaming is shallow and mostly copies source memories

`CognitiveMemoryDreamConsolidationService` selects clusters by mode and builds one aggregate candidate per selected cluster. The canonical text is a short bullet list from source record summaries/canonical text, truncated to 8 records.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` lines 476-496: mode selection is driven by readiness and simple key-family checks.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` lines 537-565: canonical aggregate text is `Synthesis from N source-backed memory record(s)` plus copied bullet lines.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` lines 584-596: claim text comes from existing memory summary/canonical/title, not from a validated abstraction step.

Why this is a problem:

- The dream run can be suspiciously fast because it does not compare source memories deeply.
- It does not derive a new concept, resolve contradictions, split mixed clusters, or create a concise aggregate meaning.
- It does not warn that only the first 8 source records were included in text when the cluster is larger.
- Broad clusters can generate meaningless syntheses and still pass validation if every copied claim has a source map.

## Finding F-03: Dream validation checks plumbing, not semantic quality

`CognitiveMemoryDreamValidator` checks missing source maps, weak evidence by count, contradiction flags, stale/superseded sources, restricted/redacted sources, access policy, and all-machine-generated support.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs` lines 99-180: issue detection is rule-based and limited to map existence/status.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs` lines 183-193: only missing source maps reject; most other issues become human review.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs` lines 60-83: review item is attached to the run subject, while the candidate receives the review id.

Missing validation behavior:

- No overbroad cluster detection.
- No cluster cohesion threshold.
- No independent evidence/source-system diversity threshold.
- No semantic support/entailment check between aggregate claim and source evidence.
- No duplicate aggregate detection.
- No check that curator corrections invalidated candidate inputs.
- No validation that an aggregate claim is less noisy and more useful than its source records.

## Finding F-04: Aggregate application is overconfident

`CognitiveMemoryAggregateMemoryApplicator` creates a machine-generated memory record and marks every aggregate claim as validated, strong accept, and display belief score 1.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs` lines 99-125: applied memory is machine-generated, approved, active, strong confidence/activation.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs` lines 137-157: every claim becomes `Validated`, `StrongAccept`, and `DisplayBeliefScore = 1`.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs` lines 206-226: mutation command kind is `ProposeClaim` even after accepted aggregate application.

Why this is a problem:

- Approval by current weak validator is not enough to justify maximum confidence.
- Similar aggregates can be created from overlapping broad clusters without dedupe.
- Source records are linked, but aggregate lineage does not clearly model “synthesizes these memories and claims.”
- There is no automatic invalidation/revalidation when a curator correction supersedes source memories.

## Finding F-05: Recall synthesis is not user/agent-facing synthesis yet

`CognitiveMemoryRecallSynthesisService` selects only context sections marked `SelectedMemory`, groups them by normalized title, takes the first line of each section, and returns bullet statements. References are hidden by default, which is good, but the statements are not meaningfully synthesized.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` lines 24-56: grouping by section title and first-line extraction.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` lines 59-138: persistence stores statement-source maps.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs` is available for reference-on-demand but only resolves persisted statement maps.

Missing behavior:

- No concise answer-oriented memory brief tuned to the requester intent.
- No removal of redundant/low-utility details except title grouping.
- No phrase-level mapping from synthesized statement parts to source claims.
- No provenance expansion through aggregate memory back to original sources.
- No proof that this synthesis is integrated into the agent-facing recall path rather than only exposed as an optional service.

## Finding F-06: Previous SelectFocus bug appears fixed

The prior issue where `SideContext` candidates could be converted into selected focus appears fixed. `CognitiveMemoryRecallEvaluation.SelectFocus` now preserves `Inhibited`, `SideContext`, and `Excluded` before selecting focus candidates.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallEvaluation.cs` lines 75-91.

This should remain protected by regression tests.

## Finding F-07: Curator/professor mode is captured but not assimilated

`CognitiveMemoryCuratorConversationService` supports direct LLM and agent conversations, deep source-grounded recall, turn recording, trusted capture, and direct application of a trusted memory. This is useful plumbing. It is not yet the desired professor-student learning behavior.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 68-147: curator send performs recall, obtains a response, and records the turn.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 484-701: trusted improvement creates source manifest/item/evidence/candidate and applies it immediately.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 704-731: capture-kind detection is English substring-based.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 736-774: affected memories are resolved from explicit request plus all included recall source refs/candidates/context sections.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 776-819: corrections mark all affected records as superseded/stale.

Missing professor behavior:

- No structured professor assertion model with claim-level target, scope, confidence, and assimilation state.
- No temporary high-trust anchor lifecycle.
- No repeated comparison of professor anchors against existing clusters and aggregates during dream runs.
- No targeted invalidation/revalidation of affected clusters and dream candidates.
- No fading/retirement of professor turn records after the knowledge has been internalized into stable derived memories.
- No multilingual/Czech capture semantics even though user interaction may be in Czech.

## Finding F-08: Curator correction targeting can damage unrelated memories

When a correction is recorded with a recall trace, the service can include all memory records that appeared in the trace/context. A single correction then marks every affected memory as `Superseded` and `Stale`.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 105-128: `SendAsync` passes all included memory ids as affected ids.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 736-774: `ResolveIncludedMemoryRecordIdsAsync` aggregates source refs, selected candidates, and context sections.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` lines 790-818: all affected records are superseded or refined.

This must be treated as a critical safety issue. A professor correction should target an explicit claim or a small target set with confidence. Ambiguous targets must become a review item or a clarification question, not a broad supersede.

## Finding F-09: Curator UI exposes capture state but not target control

The curator tab can start a session, send a message, use voice mode, show transcript, and show captured improvements. It does not expose explicit memory/claim target selection, capture type selection, scope selection, or a review/confirm panel for ambiguous captures.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor` lines 27-74: session controls.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor` lines 82-118: message/voice controls.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor` lines 188-224: trusted capture state display.
- `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Curator.cs` lines 145-149: UI sends no explicit capture kind or target selection.

This is acceptable for a prototype but not for a reliable professor learning mode.

## Finding F-10: Existing tests prove plumbing, not quality

The current tests check that clusters are created, dream runs create candidates, restricted content routes to review, aggregate application persists provenance, recall synthesis hides references by default, and curator capture creates/applies trusted records. They do not yet prove the deeper behavior requested by the user.

Relevant code:

- `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` lines 98-111 assert broad cluster families exist.
- `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` lines 262-306 test trusted new knowledge happy path.
- `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` lines 308-360 test correction with one affected recall memory.
- `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` lines 461-502 assert agent mode uses auto-approved tool calls.

Missing tests:

- Broad project/month/access clusters are not aggregate-eligible by themselves.
- Mixed-topic clusters split or fail validation.
- Duplicate/overlapping aggregates are deduped.
- Curator correction with multiple recall context records does not supersede all records.
- Czech curator phrases are captured correctly or fall back to structured UI selection.
- Professor anchors improve cluster/dream quality and later fade after assimilation.
- Recall synthesis returns a concise brief and can expand references on demand through aggregate provenance.
