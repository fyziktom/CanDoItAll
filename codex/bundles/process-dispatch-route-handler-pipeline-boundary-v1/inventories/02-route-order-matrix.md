# Route Order Matrix

| Order | Stage | Meaning | Side effect class | Risk |
| --- | --- | --- | --- | --- |
| 1 | `FreshRecoverySkip` | Skip fresh redispatch in grace period | Read route snapshot; log; complete dispatch | Route order drift |
| 2 | `DatabaseRequirement` | Block automation when PostgreSQL requirement fails | Transition step to blocked/failed per policy | Transition side effects hidden |
| 3 | `UpstreamMaterialization` | Request upstream missing artifact materialization | Journal/rerun request/transition | Repeated materialization |
| 4 | `StrandedArtifactRecovery` | Recover missing completion artifacts | Finalizer context + transition | Finalizer behavior drift |
| 5 | `Subprocess` | Observe or complete subprocess | Child run observe, gap block, projection, parent transition | Projection/capability loss |
| 6 | `StartTransition` | Move step to InProgress if needed | Transition + reload candidate fallback | Losing reload fallback |
| 7 | `Workflow` | Run/observe workflow executor | Workflow coordinator + finalizer | Workflow/direct order drift |
| 8 | `DirectAgentExecution` | Run direct agent until settled | Agent execution + recovery | Finalizer or retry drift |
| 9 | `CompetingExecutionGuard` | Prevent stale transition when competing execution active | Read execution runs | False completion |
| 10 | `RunClosedGuard` | Skip completion if run became terminal | Read run/step status | Terminal overwrite |
| 11 | `FinalizerTransition` | Finalize and apply step transition | Finalizer + transition application | Lost claim/finalizer gap |
