# Projects UI component tests

This .NET 10 xUnit/bUnit project tests the actual Projects P1 renderer and isolated sandbox
closure. It covers raw input and validation across all five mounted steps, unblurred text,
submission capture before asynchronous validation, retired forms, held mutation admission,
accepted child identities and newer or replaced local rows. Boundary tests reject forbidden
transitive dependencies, unresolved edges and implementation types in the public seam.

```powershell
dotnet test tests/Components/CanDoItAll.Projects.UI.Tests/CanDoItAll.Projects.UI.Tests.csproj --configuration ProjectsUiProof --list-tests /m:1
dotnet test tests/Components/CanDoItAll.Projects.UI.Tests/CanDoItAll.Projects.UI.Tests.csproj --configuration ProjectsUiProof --no-build --no-restore
```

The suite requires no database. Native save, lifetime, seed, deletion, package and Files
ownership are proved separately by ProjectsPage/ProjectsEditorMutation tests and actual Web
journeys. See [Testing](../../../docs/testing.md) and the
[P1 record](../../../docs/architecture/projects-portfolio-ui-p1.md) for exact proof boundaries.
