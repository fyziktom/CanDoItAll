# Agent editor core renderer tests

This small test assembly references the independent sandbox and Editor.UI, without the
application's database or module implementation graph. It tests all ten navigation identities,
all ten real sections and child renderers, one form/draft, Unicode input before
blur, current presentation updates, origin retirement, independent editors, save outcomes,
and assembly/public-contract boundaries with forbidden-transitive and unresolved controls.
Memory ordering/removal, missing references, the actual small confirmations and verification
recovery exercise the same presentation policy as the production host.

```powershell
dotnet test tests/Components/CanDoItAll.AgentFramework.Editor.UI.Tests/CanDoItAll.AgentFramework.Editor.UI.Tests.csproj --list-tests
dotnet test tests/Components/CanDoItAll.AgentFramework.Editor.UI.Tests/CanDoItAll.AgentFramework.Editor.UI.Tests.csproj --no-build --no-restore
```

Use the same configuration for both commands. Actual host authority and persistence are
covered by the main component and integration assemblies, including
`AgentEditorCoreRoundTripTests`, the existing command/session suites and native adapters.
See [Testing](../../../docs/testing.md) and the
[A2 record](../../../docs/architecture/agent-editor-completion-a2.md).
