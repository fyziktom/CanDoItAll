# SB02 Reinstall Tooling And Artifact Cleanup

## Status

- `Completed`

## Objective

Update the main repository resetup script so MCP binaries are built from `C:\repositories\CanDoItAll.Mcp` while settings, install output, and skill sync remain rooted in `C:\repositories\CanDoItAll`; then remove stale MCP install and shadow artifacts.

## Covered Inputs

- `N001`: `reinstall mcp script... updated path for the MCP repo to build mcp servers there, but skills it takes from this repo`
- `N001`: `Assure then that all is possible to build and reinstall those mcps`
- `N001`: `remove from .artifacts all old installations of mcps`

## Prerequisites

- `SB01` is completed.
- `C:\repositories\CanDoItAll.Mcp\CanDoItAll.Mcp.slnx` builds.
- DotNetWatch wrapper has a stable location in the MCP repository.

## Exact Source References

- `repo://tools/Reinstall-CanDoItAllMcps.ps1`
- `repo://CanDoItAll.Mcp.DotNetWatch.settings.json`
- `repo://CanDoItAll.Mcp.Components.settings.json`
- `repo://CanDoItAll.Mcp.CodeAnalytics.settings.json`
- `repo://CanDoItAll.Mcp.SshOps.settings.json`
- `repo://codex/skills`
- `repo://.artifacts/mcp-installs`
- `repo://.artifacts/mcp-server-shadow`

## Deliverables

- Resetup script with separate main repo and MCP repo roots.
- Generated MCP config paths pointing to MCP repo wrapper or published MCP binaries.
- Cleaned MCP install and shadow artifact directories.
- Resetup transcript and install manifest proof.

## Dependency Impact

- `SB03` depends on resetup proof and artifact cleanup output.
- Active Codex MCP server setup depends on these paths being correct.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof covering shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Requires `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Implementation Steps

1. Add an explicit `$McpRepoRoot` parameter to `repo://tools/Reinstall-CanDoItAllMcps.ps1`.
2. Default `$McpRepoRoot` to a sibling `CanDoItAll.Mcp` directory when not supplied.
3. Change MCP project and DotNetWatch wrapper paths to use `$McpRepoRoot`.
4. Keep settings, install root, shadow root, VS Code config, user config, and skill sync rooted in `$RepoRoot`.
5. Update tray arguments and defaults so the tray can call the migrated wrapper.
6. Stop install-owned MCP processes and remove stale MCP install/shadow artifacts.
7. Run resetup with validation-safe skip flags and capture proof.

## Do Not Do

- Do not move skills out of `repo://codex/skills`.
- Do not delete unrelated `.artifacts` proof folders.
- Do not reinstall suppressed Processes or ProjectStructure MCPs.
- Do not silently swallow resetup errors.

## Acceptance Checklist

- Resetup exposes and uses `$McpRepoRoot`.
- MCP publish paths point to projects under `C:\repositories\CanDoItAll.Mcp`.
- Skill sync still uses `repo://codex/skills`.
- Stale MCP install and shadow artifacts are removed before new installs are produced.
- Resetup proof exists under `bundle://proof/SB02/transcripts`.

## Proof Required

- PowerShell resetup command transcript using `-McpRepoRoot C:\repositories\CanDoItAll.Mcp`.
- Install manifest review proving MCP source repo and skill source repo are distinct.
- Post-cleanup `.artifacts` directory listing.
- Source assertion transcript proving no resetup MCP build path still points to main-repo `src/CanDoItAll.Mcp.*`.
- Anti-stub audit transcript for resetup script error handling.
- Critical proof manifest and semantic invariant contract under `bundle://proof/SB02`: `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- N/A. No browser-visible UI surface changes.

## Progression Gate

- `SB03` may start only after resetup produces current MCP install outputs from the MCP repository and stale MCP install/shadow history is gone.

## Suggested Agent Prompt

Update the resetup script to build MCP servers from `C:\repositories\CanDoItAll.Mcp` while syncing skills from this repo, clean MCP artifacts, and capture resetup/cleanup proof for `SB02`.
