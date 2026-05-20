# Refactor Map

The current services are doing too much. Use this map to split behavior without losing module cohesion.

| Current service | New extracted responsibility candidates |
|---|---|
| `CognitiveMemoryClusterPlanner` | `ICognitiveMemoryClusterSignalExtractor`, `ICognitiveMemoryClusterCandidateBuilder`, `ICognitiveMemoryClusterScorer`, `ICognitiveMemoryClusterPersistenceService` |
| `CognitiveMemoryDreamConsolidationService` | `IDreamClusterSelector`, `IDreamAggregateCandidateBuilder`, `IDreamModePolicyResolver`, `IDreamRunRecorder` |
| `CognitiveMemoryDreamValidator` | `IDreamValidationRule`, `IDreamValidationRuleSet`, `IDreamReviewItemFactory` |
| `CognitiveMemoryAggregateMemoryApplicator` | `IAggregateDeduper`, `IAggregateLineageWriter`, `IAggregateConfidenceCalibrator`, `IAggregateInvalidationService` |
| `CognitiveMemoryCuratorConversationService` | `ICuratorCaptureExtractor`, `ICuratorTargetResolver`, `IProfessorAnchorService`, `ICuratorAssimilationScheduler`, `ICuratorRuntimeService` |
| `CognitiveMemoryRecallSynthesisService` | `IRecallBriefSynthesizer`, `IRecallStatementProvenanceMapper`, `IRecallReferenceExpansionService` |

Prefer internal sealed services with interfaces only where tests or alternate providers need them.
