# Route Order Contract

Canonical order from `ProcessDispatchRoutePipeline.StageOrder`:

1. FreshRecoverySkip
2. DatabaseRequirement
3. UpstreamMaterialization
4. StrandedArtifactRecovery
5. Subprocess
6. StartTransition
7. Workflow
8. DirectAgentExecution
9. CompetingExecutionGuard
10. RunClosedGuard
11. FinalizerTransition

## Acceptance

- Unit test must assert exact order.
- Source scan must verify handler factory/order follows the same sequence.
- Execution report must cite proof at every critical gate.
- No route handler may internally call a later stage without explicit test proof.
