# Service Boundary Targets

## New or clarified boundaries

- `ICognitiveMemoryProofGateAuditor` for bundle validator fixture semantics.
- `ICognitiveMemoryProfessorAcceptedUseSignalEmitter` for production accepted-use events.
- `ICognitiveMemoryProfessorAssimilationScheduler` or equivalent automation integration.
- `ICognitiveMemoryProfessorComparisonReviewService` for `Comparing` anchor resolution.
- `ICognitiveMemoryProfessorTeachingNormalizer` for multilingual/diacritic-normalized professor capture.
- `ICognitiveMemoryDreamClaimAlignmentService` for claim-slot grouping and synthesis inputs.
- `ICognitiveMemoryDreamClaimSourceMapper` for claim-specific provenance.
- `ICognitiveMemoryApproximateClusterCandidateProvider` for embedding/ranker-backed discovery.
- `ICognitiveMemoryRecallQueryContext` carried through recall and synthesis.
- `ICognitiveMemoryRecallLineageMapper` for statement-fragment-to-source mapping.

## Refactor limits

- Avoid adding more responsibilities to `CognitiveMemoryCuratorConversationService`, `CognitiveMemoryClusterPlanner`, `CognitiveMemoryDreamConsolidationService`, `CognitiveMemoryDreamSynthesis`, and `CognitiveMemoryRecallBriefComposition`.
- New production behavior must be covered by service-level tests and at least one end-to-end test.
- Compatibility constructors may remain for tests, but production DI paths must use registered services/options.
