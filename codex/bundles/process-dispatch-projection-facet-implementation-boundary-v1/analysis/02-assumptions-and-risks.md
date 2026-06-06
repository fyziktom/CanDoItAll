# Assumptions And Risks

## Critical Path Risks

1. A shallow pass may only rename `ProcessArtifactProjectionServices` while leaving one all-facet implementation.
2. Moving too much logic out of dispatcher wrappers at once can change projection matching, storage path, or candidate mutation semantics.
3. Creating production driver APIs too early can freeze the wrong abstractions.
4. Moving nested dispatch models into contracts prematurely can force large downstream changes.
5. Broad source scans may pass even if source-family order changes unless explicit order tests remain.

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
