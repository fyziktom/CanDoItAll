# Side Effect Boundary Matrix

| Side effect | Allowed owner after bundle | Forbidden owner |
| --- | --- | --- |
| Downstream transition to Blocked | `ProcessDispatchDatabaseRequirementBlocker` or `ProcessUpstreamArtifactMaterializationCoordinator` | pure planners |
| Journal add/save | `ProcessUpstreamArtifactMaterializationJournalCoordinator` | pure planners |
| `ProcessesService.RerunAgentStepAsync` | explicit upstream materialization side-effect coordinator | pure planners, candidate hydration loader |
| `SaveAgentAsync` | existing `ProcessDispatchTechnicalAgentBindingCoordinator` only | candidate factory, hydration loader |
| EF read queries | selector/loader/query helpers | pure decision records unless explicitly named query helper |
