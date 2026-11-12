# Provider Profiles UI tests

This lightweight bUnit project builds the independent Providers.UiSandbox and the complete
Providers.UI renderer without native module owners, a database or provider executors.
It checks all six tab identities, the four actual local sections, one canonical EditContext,
raw input/validation lifetime, price row identity, Thinking origins and independent editors.
Boundary tests inspect the real assembly closure and public signatures, with forbidden,
unresolved and cyclic negative controls.

```powershell
dotnet build src/Sandboxes/CanDoItAll.AgentFramework.Providers.UiSandbox --configuration Release /m:1
dotnet test tests/Components/CanDoItAll.AgentFramework.Providers.UI.Tests --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Components.ProviderProfilesUi" /m:1
dotnet test tests/Components/CanDoItAll.AgentFramework.Providers.UI.Tests --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.ProviderProfilesUi" /m:1
```

Validate discovery before execution. Native module tests and application browser journeys
remain required; these fixture tests do not prove persistence, concurrency or authority.
See [the PP1 boundary and proof record](../../../docs/architecture/provider-profiles-ui-pp1.md).
