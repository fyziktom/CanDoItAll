# Target Solution

- Add an MSBuild property gate around the repo-wide `CopyRepositoryTemplates` target so callers can opt out with `-p:CopyRepositoryTemplatesToOutput=false` while preserving the current default behavior for normal builds.
- Update `tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1` to:
  - calculate the standard `TargetDir` for the selected project/configuration with MSBuild,
  - build without `--artifacts-path`,
  - disable template copying during the MCP build,
  - copy the resulting target directory into a short shadow artifact output folder,
  - keep the manifest pointing at the copied shadow DLL.
- Update `tools\Reinstall-CanDoItAllMcps.ps1` publish/install steps to pass the template-copy opt-out property so non-DotNetWatch MCP installs do not copy repository templates either.
- Keep the existing skill-sync, config update, manifest update, shortcuts, and process cleanup flow intact.
