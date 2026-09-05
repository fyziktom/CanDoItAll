# 02a-local-mutations: Local mutation/session boundary
Status: complete; provider acceptance passed. See the final closure for the unchanged repository documentation blocker. Owned inputs: review 1-4, 12-14; proof topics 1-12 and 27-29. Scope and non-goals: root requirements/mission; no later catalog extraction execution. Sequential prerequisites/checkpoints: [plan](../../plan/architecture-checkpoints.md).

## C# Architecture Impact
Follow [boundary map](../../architecture/01-csharp-boundary-map.md), preserving accepted Providers-01 behavior unless a new regression is demonstrated.
## Boundary Ownership
Canonical commit stays in backend; target/session owns draft and operation; Razor renders and routes current intents. Child producers report typed affected scope.
## Dependency Direction
Existing projects only; no inward UI/domain dependency.
## Pattern Decision
Use only the justified value/session/adapter/change-scope records from the root architecture.
## Testability Contract
Governed proof: exact named failing-first/direct tests per [matrix](../../architecture/04-csharp-testability-plan.md), real database composition for commit claims. Freeze expected discovery before execution; direct project build, compact transcripts, source hashes/invariants and dependent flow smoke.
## Partial Class Policy
No new partial files or partial-based extraction.
## Architecture Proof Required
Before/after scoped analysis, exact source ownership removal, direct isolated-owner tests, meaningful regression, project edge review. Update proof/SBA/manifest.md and current input closure. Later phase is blocked if this boundary is not proven. Browser uses 1600x1000, unchanged composition, and the owner matrix at final closure.
