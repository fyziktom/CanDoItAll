# Core Readiness Decision Matrix

This bundle does not create Process Core. The matrix records what is ready for a later extraction discussion.

| Area | Current owner after bundle | Future Core readiness | Decision |
| --- | --- | --- | --- |
| Route order and route kind decisions | `ProcessDispatchRoutePlanner`, route handlers | High for pure route classification only | Candidate for future Core after public contracts are chosen |
| Claim acquisition, lease, heartbeat, claim-held checks | Process module application services | Low | Keep application/infrastructure-local |
| Candidate hydration and direct-agent binding | `ProcessDispatchCandidateHydrationService` and binding coordinator | Low | Keep application/infrastructure-local because it reads EF, workspace, and AgentFramework facades |
| Database requirement and upstream materialization | Route services plus pre-execution guard handler | Low-to-medium | Pure classification rules may move later; DB/profile and journal writes stay application-local |
| Start transition and step reload | `ProcessDispatchStepTransitionService` and start route handler | Medium | Transition request shaping may move later; claim/EF writes stay application-local |
| Subprocess lifecycle status mapping | `ProcessSubprocessLifecycleRules` | High | Candidate for future Core |
| Subprocess runtime observation and artifact projection | `ProcessDispatchSubprocessRuntimeService` | Low | Keep application/infrastructure-local |
| Finalizer context construction | `ProcessDispatchFinalizerContextFactory` | Medium | Pure context mapping may move later |
| Finalizer apply and transition side effects | `ProcessDispatchFinalizerApplicationService` | Low | Keep application-local |
| Run-closed guard | `ProcessDispatchRunClosureGuardService` | Low | Keep application/infrastructure-local |

## Blockers Before Core Extraction

- Public contracts for future Core-owned route and lifecycle decisions are not defined.
- EF-backed candidate hydration, claim lifecycle, artifact projection, and AgentFramework execution remain intentionally application/infrastructure-bound.
- Production process-driver APIs are explicitly out of scope and were not introduced.
