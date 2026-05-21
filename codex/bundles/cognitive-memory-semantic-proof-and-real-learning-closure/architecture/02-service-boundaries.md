# Service Boundaries To Implement

- `ICognitiveMemoryBundleProofClaimVerifier` or equivalent validator layer for claim-to-code proof labels.
- `ICognitiveMemoryProfessorTeachingIntentClassifier` for multilingual teaching intent detection.
- `ICognitiveMemoryProfessorClaimExtractor` for structured claim/scope/example/counterexample extraction.
- `ICognitiveMemoryRecallOutcomeAcceptedEventHandler` for accepted-use emission from real workflow outcomes.
- `ICognitiveMemoryEmbeddingClusterCandidateProvider` for actual embedding/ranker candidate discovery.
- `ICognitiveMemoryLexicalSignalClusterCandidateProvider` for honest deterministic fallback.
- `ICognitiveMemoryDreamDomainClaimSynthesizer` for domain claim synthesis without source-map meta text.
- `ICognitiveMemoryClaimEvidenceSupportLoader` for exact claim evidence links.
- `ICognitiveMemoryRecallTaskBriefPlanner` and `ICognitiveMemoryStatementLineageBuilder` for task-facing recall briefs.
- Options should be injected through configuration/DI rather than created through scattered `new CognitiveMemoryQualityAlgorithmOptions()` fallbacks in production paths.

