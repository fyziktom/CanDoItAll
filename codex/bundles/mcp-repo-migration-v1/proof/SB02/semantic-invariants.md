# SB02 Semantic Invariants

## Invariant SB02-RESETUP-ROOT-SEPARATION

- Invariant ID: `SB02-RESETUP-ROOT-SEPARATION`
- Source raw note: `N001` required the reinstall script to build MCP servers from the MCP repo while taking skills from this repo.
- Expected behavior: `Reinstall-CanDoItAllMcps.ps1` accepts `-McpRepoRoot`, builds MCP projects and the DotNetWatch wrapper from that root, but keeps settings, install artifacts, user config, VS Code config, and skill sync rooted in `-RepoRoot`.
- Disallowed shallow implementation: Adding a parameter but still resolving MCP build paths from the main repo, or moving/syncing skills from the MCP repo.
- Failing-first test: N/A - process/non-production tooling migration; no application production behavior changed. Negative proof is the source/config assertion transcript that fails when roots are mixed.
- Passing test: `bundle://proof/SB02/transcripts/resetup.txt`, `bundle://proof/SB02/transcripts/source-and-config-assertions.txt`, and `bundle://proof/SB02/transcripts/wrapper-config-integration-tests.txt`.
- Changed source files: `repo://tools/Reinstall-CanDoItAllMcps.ps1`, `repo://CanDoItAll.Mcp.DotNetWatch.settings.json`; MCP repo local context only: `tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1`, `tools/CanDoItAll.Mcp.DotNetWatch.Tray/TrayOptions.cs`, `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/ValidationHarness.cs`, `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/BootstrapValidationTests.cs`.
- Production assertions: N/A - machine setup and MCP host tooling only.
- Red-team negative case: `bundle://proof/SB02/transcripts/source-and-config-assertions.txt` fails if generated manifest/config omit `-McpRepoRoot` or if skill source root is not the main repo.
- Downstream dependency check: `SB03` docs and final closure cite the resetup transcript and install manifest assertions.

## Invariant SB02-ARTIFACT-CLEANUP

- Invariant ID: `SB02-ARTIFACT-CLEANUP`
- Source raw note: `N001` required removing old MCP installations and historical build artifacts from `.artifacts`.
- Expected behavior: Resetup removes historical `mcp-installs` and DotNetWatch shadow artifacts before publishing current outputs, and stale MCP traces outside the live install/shadow roots are absent.
- Disallowed shallow implementation: Publishing new outputs while leaving retired Processes/ProjectStructure MCP installs or old MCP build directories in generated artifact history.
- Failing-first test: N/A - process/non-production artifact cleanup; no application production behavior changed. Negative proof is the cleanup assertion transcript that rejects MCP traces outside live roots and retired MCP names.
- Passing test: `bundle://proof/SB02/transcripts/resetup.txt` and `bundle://proof/SB02/transcripts/artifact-cleanup.txt`.
- Changed source files: `repo://tools/Reinstall-CanDoItAllMcps.ps1`.
- Production assertions: N/A - local generated artifacts only.
- Red-team negative case: `bundle://proof/SB02/transcripts/artifact-cleanup.txt` fails if `CanDoItAll.Mcp.Processes` or `CanDoItAll.Mcp.ProjectStructure` remains in live installs, shadow artifacts, VS Code config, or Codex config.
- Downstream dependency check: `SB03` docs describe only `mcp-installs` and `mcp-server-shadow` as current MCP runtime artifact roots.
