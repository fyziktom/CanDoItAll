# Projects Files renderer tests

These light tests render both Projects Files views with real neutral FileBrowser
sessions and read-only content from the independent sandbox. They cover actual fixture
viewer families, error/empty states, source removal, bounded paging/search, independent
preview lifetime and the evaluated assembly boundary. They reference neither the
production Projects module nor its file-authority implementation.

```powershell
dotnet test tests/Components/CanDoItAll.Projects.Files.UI.Tests --configuration ProjectsFilesProof --list-tests --filter FullyQualifiedName~CanDoItAll.Tests.Components.ProjectsFilesUi /m:1
dotnet test tests/Components/CanDoItAll.Projects.Files.UI.Tests --configuration ProjectsFilesProof --no-build --no-restore --filter FullyQualifiedName~CanDoItAll.Tests.Components.ProjectsFilesUi /m:1
```

The first command builds and confirms discovery before the second uses those outputs.
Real authorization, native workspace replacement, stream/JS cleanup and browser download
proof remain in the owning Unit, Components and Playwright projects. See the
[P2 evidence record](../../../docs/architecture/projects-files-ui-p2-evidence.md).
