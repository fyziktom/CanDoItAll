# Target Solution

## Target Architecture Layers

```mermaid
flowchart TD
    A[Artifact-backed bundle workflow] --> B[Regression-first semantic corpus]
    B --> C[Composite semantic clustering]
    C --> D[Dream claim synthesis]
    D --> E[Entailment and aggregate validation]
    E --> F[Calibrated aggregate application]
    G[Professor curator conversation] --> H[Structured temporary professor anchors]
    H --> C
    H --> D
    H --> I[Assimilation and fading lifecycle]
    F --> J[Recall synthesis]
    I --> J
    J --> K[Reference-on-demand lineage]
    K --> L[Red-team end-to-end proof]
```

## Behavioral Model

1. Source memories are indexed into composite semantic signals.
2. Clustering creates bounded candidate pairs from exact keys, aliases, rare token phrases, evidence overlap, relation edges, and optional embedding/provider similarity.
3. Components are evaluated for internal cohesion. Weak bridge chains are split or routed to review.
4. Dreaming transforms coherent clusters into aggregate claim groups. It does not merely choose one representative source sentence.
5. Validation checks claim coverage, conflict separation, independence, access policy, duplicate aggregates, professor-anchor lifecycle, and synthesis-vs-copy quality.
6. Professor/curator mode captures human guidance as temporary structured anchors. Anchors can influence comparison and review, but are not ordinary stable knowledge until assimilation criteria pass.
7. Assimilation requires independent non-descendant evidence and repeated successful integration into clusters/dreams/recall.
8. Recall synthesizes a brief suited to the task and policy context. References remain hidden by default but are precisely resolvable on demand.

## Required Collaborators

- `ICognitiveMemoryProofManifestValidator`
- `ICognitiveMemoryClusterKeyExtractor`
- `ICognitiveMemoryCandidatePairSelector`
- `ICognitiveMemoryClusterCohesionSplitter`
- `ICognitiveMemoryDreamClaimSynthesizer`
- `ICognitiveMemoryDreamEntailmentValidator`
- `ICognitiveMemoryProfessorTeachingExtractor`
- `ICognitiveMemoryProfessorAssimilationEvaluator`
- `ICognitiveMemoryRecallBriefComposer`
- `ICognitiveMemoryStatementLineageResolver`

## Algorithm Versioning

Every changed behavior must have an explicit algorithm version and tests must assert the intended version where records are persisted.
