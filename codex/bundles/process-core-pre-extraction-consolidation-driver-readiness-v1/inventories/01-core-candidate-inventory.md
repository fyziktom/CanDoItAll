# Core Candidate Inventory

Candidate | Current owner | This bundle action | Later Core eligibility
---|---|---|---
Route stage order | ProcessDispatchRoutePipeline | Stabilize source-payload-free descriptors | High
Route eligibility | ProcessDispatchRouteEligibility / route planner | Lock pure rule tests | High
Subprocess lifecycle mapping | ProcessSubprocessLifecycleRules | Separate runtime/persistence side effects | High
Transition request shaping | planners/rules | Prove pure request builders only | Medium
Artifact expectation matching | artifact expectation snapshots/rules | Stabilize shared snapshot and pure rules | High
Artifact satisfaction | satisfaction rules | Keep pure matching separate from projection/storage | Medium-high
Finalizer intent mapping | finalizer DTOs/adapters | Separate intents from application finalizer | Medium
Driver verification lanes | docs only | Keep docs/test-only | Later
