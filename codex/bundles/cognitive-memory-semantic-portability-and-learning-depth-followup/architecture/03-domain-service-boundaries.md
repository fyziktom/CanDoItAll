# Domain Service Boundaries

## Current issue

The latest implementation extracted useful collaborators, but several orchestration services remain too large and mix persistence, algorithmic policy, formatting, mutation, and lifecycle transitions.

## Target boundaries

- `ClusterPlanningOrchestrator`: loads records, invokes candidate generation, persists plans.
- `ClusterCandidateDiscovery`: exact-key blocking, approximate semantic neighbors, pair-budget enforcement.
- `ClusterQualityScorer`: edge scoring, coverage scoring, aggregate eligibility.
- `DreamRunOrchestrator`: selects clusters, delegates synthesis, validation, apply/review.
- `DreamClaimGrouper`: claim signatures, slots, source roles.
- `DreamClaimSynthesizer`: deterministic structured synthesis.
- `DreamEntailmentValidator`: contradiction and support validation.
- `ProfessorConversationExtractor`: turn-level and multi-turn teaching extraction.
- `ProfessorAnchorLifecycleService`: state transitions and audit.
- `ProfessorMasteryEvaluator`: event-backed mastery, repeated-use and integration proof.
- `RecallBriefPlanner`: answer/action/caveat statement planning.
- `RecallLineageResolver`: statement-to-claim-to-source expansion.

## Options

Algorithm options must be injected through `IOptions<CognitiveMemoryQualityAlgorithmOptions>` or equivalent typed configuration. Static `Current` can remain only as a default factory, not as production runtime access in services.
