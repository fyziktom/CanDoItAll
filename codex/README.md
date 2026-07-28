# Codex Integration And Evidence

The canonical CanDoItAll Codex development skills, architecture support assets, and plugins live in the sibling [CanDoItAll.SharedInfo](https://github.com/fyziktom/CanDoItAll.SharedInfo) repository under `codex`.

This repository owns its installer adapter, product-specific bundle evidence, and integration configuration. It does not vendor SharedInfo-owned Codex sources.

## Install Or Refresh

Place `CanDoItAll.SharedInfo` beside this repository, then run from this repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1 -SharedInfoRepoRoot ..\CanDoItAll.SharedInfo
```

The script:

- delegates CanDoItAll skill installation to the canonical SharedInfo installer
- installs SharedInfo-owned support folders required by those skills
- installs the required public skills from `openai/skills` and `dotnet/skills`
- writes into `$CODEX_HOME\skills`, or the standard user Codex home when `CODEX_HOME` is unset

The script fails explicitly when the SharedInfo installer is unavailable. It does not fall back to the historical mirror.

Useful switches:

- `-SkipCustomSkills` installs only the selected public skills
- `-SkipPublicSkills` installs only SharedInfo-owned skills
- `-CodexHome <path>` selects another Codex home
- `-SharedInfoRepoRoot <path>` selects the canonical SharedInfo checkout

`CANDOITALL_SHAREDINFO_ROOT` can provide the SharedInfo path when the command-line parameter is omitted.

## MCP Setup

The full local MCP reinstall also refreshes canonical skills unless `-SkipSkillSync` is supplied:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Reinstall-CanDoItAllMcps.ps1 -McpRepoRoot ..\CanDoItAll.Mcp -SharedInfoRepoRoot ..\CanDoItAll.SharedInfo
```

MCP server source is owned by the sibling `CanDoItAll.Mcp` repository. Place `CanDoItAll.CodeAnalysis` beside that repository because the reinstall script requires its CodeAnalytics application project. The active sidecars are CodeAnalytics, Components, DotNetWatch, Mermaid, and SshOps.

## Ownership Boundary

- `codex/bundles` and `.codex/bundles` are execution evidence snapshots.
- `Templates` contains app-owned seed inputs for internal agents, capabilities, processes, and workflows. Those runtime templates are not Codex development skills and must remain in this repository.
- SharedInfo owns reusable Codex skills, including bundle workflow skills used by developers and operators; managed app agents use the app's processes and workflows instead.
- test counts, warnings, paths, package versions, and architecture claims inside completed bundles describe the recorded execution, not necessarily the current branch.

Maintained contributor guidance starts at the [repository README](../README.md) and [documentation index](../docs/README.md). When current behavior changes, update maintained docs and the canonical SharedInfo skill in its owning repository; do not rewrite closed evidence to look current.
