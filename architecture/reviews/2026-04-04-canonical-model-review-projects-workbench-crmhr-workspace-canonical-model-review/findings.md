# Findings Scratchpad

- Critical
  - `ProjectPartyAssignment` persists `ProjectId` plus plain `NodeKey` in `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`.
  - `ProjectPartyIntegrationService.SaveAssignmentAsync` validates the node only on write, then stores the string key in `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`.
  - `ProjectWorkbenchService.DeleteObjectAsync` and `MoveDescendantsToProjectAsync` in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` never touch CRM/HR assignment rows.
- High
  - `ProjectObjectRecordConfiguration.Configure` only guarantees `{ ProjectId, NodeKey }` uniqueness in `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`.
  - `ProjectNodeScopeBridge.ResolveAsync` resolves by `NodeKey` only in `src/CanDoItAll.Modules.Workbench/ProjectNodeScopeBridge.cs`.
  - `ProjectObjectType`, `ProjectObjectRecord`, and `ProjectObjectMetadataEnvelope` are carrying too many concept families across shared Workbench infrastructure.
- Medium
  - `ProjectPartyAssignmentRole` and `ProjectPartyAssignmentKind` are duplicated across Projects and CRM/HR.
  - Both role mapping methods fail open to `TechnicalContact`.
  - `ProjectStructureAgentAdministrationService` owns authorization policy in Workspace rather than a clearly documented security boundary.
