# Shared provider UI tests

Independent bUnit tests render the same Sharing, Sources, catalog and refresh children as production
using the sandbox's separate stored fixture, without the native module, database or provider runtime.
They cover immutable submissions, later input, unknown outcomes, delivery, validation and two editors.
Graph and public-signature guards cover the full sandbox/leaf closure and forbidden transitive
dependencies, unresolved edges and cycles. PostgreSQL-backed native owner and host tests remain
in [CanDoItAll.Tests.Components](../CanDoItAll.Tests.Components/README.md).

Run the focused family from the repository root:

```powershell
dotnet test tests/Components/CanDoItAll.AgentFramework.SharedProviders.UI.Tests --filter FullyQualifiedName~CanDoItAll.Tests.Components.SharedProvidersUi
```

These tests are included in the Components and Stable solution filters and the CI component lists.
See [Testing](../../../docs/testing.md) for build, discovery and portability closure requirements.
