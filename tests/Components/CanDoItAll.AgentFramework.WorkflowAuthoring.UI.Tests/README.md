# Workflow authoring component tests

This assembly references the extracted UI without native module services. It checks
accepted save reconciliation, later edits and ABA revisions, target retirement,
preview snapshots and unknown admission, late project reads, raw invalid values,
configuration preservation, exact source model routes and Prompt binding lifetime.
Native persistence, authority and page composition tests remain in
`CanDoItAll.Tests.Components`; browser and multi-instance proof are separate.

Build-backed discovery precedes execution:

```powershell
dotnet test tests/Components/CanDoItAll.AgentFramework.WorkflowAuthoring.UI.Tests -c Debug --list-tests
dotnet test tests/Components/CanDoItAll.AgentFramework.WorkflowAuthoring.UI.Tests -c Debug --no-build --no-restore
```

The project is registered in the Components and Stable test solutions and all
applicable CI component selections. It is not a product-solution project.
