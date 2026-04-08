# Notes

## Commands

- `dotnet build CanDoItAll.slnx`
  - Success
  - Warnings: unrelated `NU1510` in `CanDoItAll.Mcp.DotNetWatch.csproj`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectPartyAssignmentIntegrationTests"`
  - Success
  - 22 tests passed

## Key symbol evidence

- `CanDoItAll.Modules.Workbench.ProjectWorkbenchService.DeleteObjectAsync`
- `CanDoItAll.Modules.Workbench.ProjectWorkbenchService.MoveDescendantsToProjectAsync`
- `CanDoItAll.Modules.Workbench.ProjectNodeScopeBridge.ResolveAsync`
- `CanDoItAll.Modules.Workbench.ProjectObjectRecord`
- `CanDoItAll.Modules.Workbench.ProjectObjectMetadataEnvelope`
- `CanDoItAll.Modules.CrmHr.ProjectPartyAssignment`
- `CanDoItAll.Modules.CrmHr.ProjectPartyIntegrationService.SaveAssignmentAsync`
- `CanDoItAll.Modules.CrmHr.ProjectPartyIntegrationService.ValidateNodeScopeAsync`
- `CanDoItAll.Modules.CrmHr.ProjectPartyIntegrationService.MapRole(ProjectPartyAssignmentRole)`
- `CanDoItAll.Modules.CrmHr.ProjectPartyIntegrationService.MapRole(ProjectPartyAssignmentKind)`
- `CanDoItAll.Modules.Workspace.ProjectStructureAgentAdministrationService`
