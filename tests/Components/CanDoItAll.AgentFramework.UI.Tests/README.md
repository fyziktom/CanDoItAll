# AgentFramework UI tests

Independent bUnit and state tests for the complete Request History workspace in
`src/UI/CanDoItAll.AgentFramework.UI`. The project consumes the real AgentFramework
sandbox and its narrow synthetic History reader. It does not boot the production
module, database, provider runtime or authorization implementation.

The History selection contains 84 cases: query state, presentation seams, raw
filter validation, workspace/action retirement and all sandbox scenarios. Native
authentication/profile wiring remains in `ProviderRequestHistoryPanelTests` in the
main Components test project. Backend authorization and canonical-owner persistence
remain in Integration; real producer/content proof remains in the native browser lane.

`TeamAuthoringTests` adds 23 independent cases for the real metadata/icon/member family,
immutable submissions, late callbacks, read-only recovery and all team sandbox scenarios.
Use configuration `AgentAuthoringCa1` and `FullyQualifiedName~TeamAuthoringTests` for this
bounded selection after directly building the UI and sandbox. Native team ownership and
catalog integration remain in Unit and the main Components test project.

Build the production UI and sandbox first, then discover and run this owning project:

```powershell
dotnet test tests/Components/CanDoItAll.AgentFramework.UI.Tests/CanDoItAll.AgentFramework.UI.Tests.csproj --configuration HistoryPp3Proof --list-tests /m:1
dotnet test tests/Components/CanDoItAll.AgentFramework.UI.Tests/CanDoItAll.AgentFramework.UI.Tests.csproj --configuration HistoryPp3Proof --no-build --no-restore /m:1
```

See [Testing](../../../docs/testing.md#request-history-pp3) and the
[PP3 execution record](../../../docs/architecture/provider-history-ui-pp3.md).
