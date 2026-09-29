# Workspace UI boundary tests

These xUnit and bUnit tests exercise the real four Settings renderers and their presentation
controllers through controlled in-memory owners. They depend on the standalone scenario
host, not the production Web module or shared database test harness.

```powershell
dotnet test tests/Components/CanDoItAll.Workspace.UI.Tests --configuration WorkspaceSettingsUiProof --list-tests --filter FullyQualifiedName~WorkspaceUi /m:1
dotnet test tests/Components/CanDoItAll.Workspace.UI.Tests --configuration WorkspaceSettingsUiProof --no-build --no-restore --filter FullyQualifiedName~WorkspaceUi /m:1
```

See [the proof record](../../../docs/architecture/workspace-settings-core-ui-boundary.md)
for current case counts, real owners and hosted/browser validation. Negative graph fixtures
reject forbidden transitive and unresolved dependencies. Raw input, exact editor readiness,
unknown outcomes and successor protection are behavioral assertions, not snapshot counts.
