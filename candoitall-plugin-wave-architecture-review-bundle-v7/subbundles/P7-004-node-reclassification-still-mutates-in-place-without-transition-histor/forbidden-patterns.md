# Forbidden patterns

The following patterns must be removed or made impossible:
- ReclassifyObjectAsync directly assigning node.ObjectType = request.TargetObjectType
- no transition history record for note -> richer-block evolution

## Evidence anchors
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975
- tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:949-1002
