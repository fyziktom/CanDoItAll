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

Controlled scenario tests cover retired admitted replies, independently pending successor
writes, section/filter intent, delayed reconciliation with empty/remaining unread results,
and disposal of writes and reads. They use the real scenario store and no production owner.

The existing Components project separately exercises the real routed host and shell over
PostgreSQL. Its `CollaborationReconciliationTests` bind the real renderer to the production
session with a controlled owner: both change and input-without-blur preserve successor
drafts, and a disabled Mark-read button is demonstrably not an accepted completion.
See [Testing](../../../docs/testing.md) and the
[boundary record](../../../docs/architecture/collaboration-ui-boundary.md).
