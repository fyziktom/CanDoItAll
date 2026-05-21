# Current State

- `C:\repositories\CanDoItAll\Directory.Build.targets` defines `RepositoryTemplateContent Include="$(MSBuildThisFileDirectory)Templates\**\*.*"` and a `CopyRepositoryTemplates` target that runs `AfterTargets="Build"` for every project when template files exist.
- `C:\repositories\CanDoItAll\tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1` computes a shadow build root under `.artifacts\mcp-server-shadow\builds\<64-char-source-signature>-<timestamp>` and currently runs `dotnet build ... --artifacts-path $buildRoot`.
- The shadow build path causes referenced projects such as `CanDoItAll.Mcp.Core` to receive an output folder under the artifact root, then the repo-wide template copy target appends `Templates\Agents\teams\visual-automation-templates\members\screenshot-review-storage-agent`.
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1` prepares DotNetWatch through the wrapper, then publishes Components, CodeAnalytics, SshOps, Manager, and Tray into `.artifacts\mcp-installs\...\current`, and syncs repo-managed skills from `codex\skills` into the user skill root.
- MCP project references inspected for the failure path are limited to MCP projects and framework/package references; the template copy is injected by the shared target rather than by an MCP project needing template content.
