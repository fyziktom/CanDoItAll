# SB06 Semantic Invariants

## Invariant SB06-DREAM-TEXT-01

- Invariant ID: `SB06-DREAM-TEXT-01`
- Source raw note: Dream summaries must be useful knowledge, not diagnostic boilerplate.
- Expected behavior: Synthesized dream text uses claim, evidence, condition, and caveat sections with domain content.
- Disallowed shallow implementation: Storing internal evidence-count phrasing as final memory knowledge.
- Failing-first test: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate` failed in SB02 baseline.
- Passing test: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate` and updated `SemanticInvariant_DreamClaimSynthesisProducesStructuredSlots`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs`.
- Production assertions: The synthesizer no longer emits the old diagnostic headers and produces `Claim`, `Evidence`, `Condition`, and `Caveat`.
- Red-team negative case: Tests assert diagnostic boilerplate is absent from canonical aggregate text.
- Downstream dependency check: Aggregate memories are fit for recall brief composition.

## Invariant SB06-CLAIM-SOURCE-MAPS-02

- Invariant ID: `SB06-CLAIM-SOURCE-MAPS-02`
- Source raw note: Per-claim provenance must not assign every record source to every claim.
- Expected behavior: Source maps are created by `CreateClaimSpecificSourceMaps` for each `DreamClaimGroup`.
- Disallowed shallow implementation: Broadly flattening every claim unit source map into every aggregate claim.
- Failing-first test: `SemanticInvariant_DreamConsolidationCreatesClaimSpecificSourceMaps` failed in SB02 baseline.
- Passing test: `SemanticInvariant_DreamConsolidationCreatesClaimSpecificSourceMaps`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`.
- Production assertions: Source-map keys deduplicate by source memory, source item, evidence anchor, and direction within the current claim group.
- Red-team negative case: Source audit rejects the old broad flattening expression.
- Downstream dependency check: Reference resolver receives claim-specific aggregate source maps.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Dream aggregate claim source maps | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs` | `bundle://proof/SB06/transcripts/anti-stub.txt` |
