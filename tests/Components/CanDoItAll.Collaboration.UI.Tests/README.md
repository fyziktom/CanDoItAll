# Collaboration renderer tests

Lightweight bUnit tests of the real renderer, forms, BaseLib children and sandbox scenarios.
The project references the sandbox/UI/contracts closure and no production module or Web
host. Dependency guards reject forbidden transitive references, unresolved feature
dependencies and inappropriate public contract types. Framework assemblies are the explicit
traversal boundary; evaluated project/package graphs provide the complementary build proof.

```powershell
dotnet test tests/Components/CanDoItAll.Collaboration.UI.Tests/CanDoItAll.Collaboration.UI.Tests.csproj --configuration Release --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Components.Collaboration." /m:1
dotnet test tests/Components/CanDoItAll.Collaboration.UI.Tests/CanDoItAll.Collaboration.UI.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Components.Collaboration." /m:1
```

The existing Components project separately exercises the real routed host and shell over
PostgreSQL. See [Testing](../../../docs/testing.md) and the
[boundary record](../../../docs/architecture/collaboration-ui-boundary.md).
