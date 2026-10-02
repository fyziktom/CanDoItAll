# Shared provider UI tests

Independent bUnit tests render the same Sharing children as production without the native module,
database or provider runtime. Graph and public-signature guards include forbidden transitive
dependencies, unresolved edges and cycles. PostgreSQL-backed native owner and host tests remain
in [CanDoItAll.Tests.Components](../CanDoItAll.Tests.Components/README.md).

Run the focused family from the repository root:

```powershell
dotnet test tests/Components/CanDoItAll.AgentFramework.SharedProviders.UI.Tests --filter FullyQualifiedName~CanDoItAll.Tests.Components.SharedProvidersUi
```

These tests are included in the Components and Stable solution filters and the CI component lists.
See [Testing](../../../docs/testing.md) for build, discovery and portability closure requirements.
