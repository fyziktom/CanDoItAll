# SB07 Semantic Invariants

## Invariant SB07-APPROXIMATE-PROVIDER-01

- Invariant ID: `SB07-APPROXIMATE-PROVIDER-01`
- Source raw note: Approximate clustering must use a production provider boundary and expose deterministic continuation diagnostics.
- Expected behavior: Candidate selection delegates approximate pair discovery to `ICognitiveMemoryApproximateClusterCandidateProvider`.
- Disallowed shallow implementation: Unbounded all-pairs comparison hidden inside the selector.
- Failing-first test: `SemanticInvariant_ClusterDiscoveryHasEmbeddingBackedApproximateCandidateProvider` failed in SB02 baseline.
- Passing test: `SemanticInvariant_ClusterDiscoveryHasEmbeddingBackedApproximateCandidateProvider`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`.
- Production assertions: Request/result records carry `ContinuationCursor`, `EmbeddingProfileId`, skipped count, and `ApproximateCandidatePairsGenerated`.
- Red-team negative case: The provider deduplicates pair keys and enforces scope before scoring.
- Downstream dependency check: Cluster planner receives bounded approximate candidate pairs.

## Invariant SB07-DI-REGISTRATION-02

- Invariant ID: `SB07-DI-REGISTRATION-02`
- Source raw note: New quality collaborators must be registered and versioned options preserved.
- Expected behavior: `ICognitiveMemoryApproximateClusterCandidateProvider` is registered in the module service graph and injected into `ICognitiveMemoryCandidatePairSelector`.
- Disallowed shallow implementation: Static singleton fallback only with no DI path.
- Failing-first test: SB02 baseline lacked provider source.
- Passing test: `CognitiveMemoryModule_RegistersQualityCollaboratorsAndVersionedOptions`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`.
- Production assertions: Module registration resolves the provider and keeps `quality-clustering-v3` options.
- Red-team negative case: Provider constructor requires a semantic similarity provider and cannot run with null collaborators.
- Downstream dependency check: SB09 maintainability proof cites the extracted provider boundary.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Approximate cluster candidate provider | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` selector | `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `bundle://proof/SB07/transcripts/anti-stub.txt` |
