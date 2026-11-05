# CanDoItAll.Editing.Tests

Focused xUnit tests for the framework-neutral
[concurrent-editing core](../../../src/Foundation/CanDoItAll.Editing/README.md).
The cases cover scalar decisions, membership and keyed-row merges, delete/edit
conflicts, bounded text merging, and the core assembly's dependency boundary.
They require no database or browser fixture.

Run the owning selector from the repository root:

```powershell
dotnet test tests/Unit/CanDoItAll.Editing.Tests/CanDoItAll.Editing.Tests.csproj -c Release --filter FullyQualifiedName~CanDoItAll.Editing.Tests.MergeTests
```

Follow [Testing](../../../docs/testing.md) for build-backed discovery, standard build
configurations, and the repository's required validation gates.
