# Projects Files UI sandbox

This independent Blazor host renders both production Files views over real neutral
FileBrowser sessions and valid fixture content: Unicode text, Markdown, JSON, Mermaid,
SVG, PNG and PDF. It requires no database, vault, production module, native launcher or
model provider. Its source-mode closure currently contains eleven projects.

From the repository root:

```powershell
npm run tailwind:build
dotnet build src/Sandboxes/CanDoItAll.Projects.Files.UiSandbox --configuration ProjectsFilesProof /m:1
dotnet watch --project src/Sandboxes/CanDoItAll.Projects.Files.UiSandbox run --configuration ProjectsFilesProof
dotnet publish src/Sandboxes/CanDoItAll.Projects.Files.UiSandbox --configuration ProjectsFilesProof --output artifacts/projects-files-sandbox-publish /m:1
```

The existing compiled application theme is a linked asset, not a Web project dependency.
If absent, build fails with the existing Tailwind preparation command. BaseLib fonts,
FileTools viewers, Mermaid and sandbox CSS isolation are served by source and published
hosts. Validate at 1920×1080 and 100% zoom.

Choose a representative, empty, large or unavailable source scenario, then Reset. Open
the independent dialog while retaining the pane. Enter opens a file; Back returns the
pane to its browser. Simulated host actions record intent without launching or
downloading. Hold next read followed by Refresh, Remove selected source and Release
reads demonstrates a retired source completing without restoring its items. Fail next
read exercises actual provider error and Retry. Reset and close revoke their own
fixture content; they do not copy the production authorization state machine.

The light `CanDoItAll.Projects.Files.UI.Tests` project is selected by Components, Stable
and CI. `ProjectsFilesSandboxBrowserTests` checks real viewers, geometry, served assets
and a separately published host. Production authority and download proof lives in the
owning tests described in [the P2 record](../../../docs/architecture/projects-files-ui-decoupling.md).
