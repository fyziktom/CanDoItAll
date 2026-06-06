# Assumptions And Risks

## Working Assumptions

- The branch starts from the completed `process-dispatch-projection-host-facet-boundary-v1` state.
- This bundle is architecture refactoring only; projection behavior and source-family order must remain unchanged.
- Browser validation remains `N/A` unless a prohibited UI file change appears, in which case the scope must be reopened.

## Critical Path Risks

- A shallow pass may only rename `ProcessArtifactProjectionServices` while leaving one all-facet implementation.
- Moving too much logic out of dispatcher wrappers at once can change projection matching, storage path, or candidate mutation semantics.
- Creating production driver APIs too early can freeze the wrong abstractions.
- Moving nested dispatch models into contracts prematurely can force large downstream changes.
- Broad source scans may pass even if source-family order changes unless explicit order tests remain.

## Validation Risks

- Projection behavior can regress silently when duplicate handling or external reference key handling changes.
- Provider-native browser artifact projection is especially sensitive to source/target file handling.
- Response-text projection is sensitive to overwrite avoidance and existing-managed artifact reuse.
- Candidate mutation must remain centralized.
- A build-only proof is insufficient.

## Reopen Triggers

Reopen the last production subbundle when any of the following occurs:
- `CanDoItAll.Processes.Core` appears.
- `IProcessDriverPack`, `IProcessDriverRegistry`, or production driver package names appear under `src`.
- UI/Razor/CSS/JS/TS files change.
- `ProcessArtifactProjectionServices` remains a single all-facet implementation after SB52.
- projection source-family order changes.
- source coordinators receive `ProcessRunAutomationDispatchService` or a broad all-facet host.
- focused projection tests fail.
- candidate mutation is duplicated outside the candidate-state facet.
