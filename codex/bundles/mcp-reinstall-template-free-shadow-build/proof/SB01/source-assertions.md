# SB01 Source Assertions

## Assertion 1 - Template Copying Is Opt-Out, Not Removed

- `repo://Directory.Build.targets` now defines `CopyRepositoryTemplatesToOutput` with a default value of `true`.
- The `CopyRepositoryTemplates` target only runs when `CopyRepositoryTemplatesToOutput` is `true`, preserving normal template-copy behavior for callers that do not opt out.

## Assertion 2 - MCP Publish Installs Opt Out Of Templates

- `repo://tools/Reinstall-CanDoItAllMcps.ps1` passes `-p:CopyRepositoryTemplatesToOutput=false` to `dotnet publish` in `Publish-ReleaseArtifact`.
- The same script still calls `Sync-RepoSkills`, still writes `skills.sourceRoot`, `skills.targetRoot`, and `skills.synced` to `.artifacts\mcp-installs\install-manifest.json`, and the passing reinstall transcript shows 18 skills synced.

## Assertion 3 - DotNetWatch Uses Standard Build Plus Artifact Copy

- `repo://tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1` no longer calls `dotnet build ... --artifacts-path`.
- The wrapper resolves `TargetDir` with `dotnet msbuild -getProperty:TargetDir`, builds the project with normal `dotnet build -c Release`, and passes `-p:CopyRepositoryTemplatesToOutput=false`.
- The wrapper copies the final build output from the standard repo `bin\Release` target directory into `.artifacts\mcp-server-shadow\builds\<short-signature>\app`.
- `Copy-DirectoryContents` excludes a top-level `Templates` directory when creating the shadow artifact, protecting the artifact even if an old local build output contains stale template content.

## Assertion 4 - Host Proof Closes The User Report

- `bundle://proof/SB01/transcripts/failing-first-current-state.txt` reproduces the pre-fix long path failure from `Directory.Build.targets`.
- `bundle://proof/SB01/transcripts/reinstall-pass.txt` proves the full reinstall path succeeds after the fix.
- `bundle://proof/SB01/transcripts/artifact-scan.txt` proves current MCP shadow/install artifacts contain no copied `Templates` directories and that the install manifest includes the skills section.
