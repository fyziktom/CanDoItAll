# Codex Skills And Evidence

The canonical CanDoItAll Codex skills live in the sibling [CanDoItAll.SharedInfo](https://github.com/fyziktom/CanDoItAll.SharedInfo) repository under `codex/skills`.

This repository owns an installer entry point, product-specific bundle evidence, and integration configuration. Its checked-in `codex/skills` tree is a historical mirror retained during repository migration. Do not edit, publish, or install that mirror as the current skill pack.

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

## What Is Historical

- `codex/skills` is a noncanonical migration mirror.
- `codex/architecture-review` and `codex/csharp-architecture` are compatibility package notes, not install sources.
- `codex/bundles` and `.codex/bundles` are execution evidence snapshots.
- test counts, warnings, paths, package versions, and architecture claims inside completed bundles describe the recorded execution, not necessarily the current branch.

Maintained contributor guidance starts at the [repository README](../README.md) and [documentation index](../docs/README.md). When current behavior changes, update maintained docs and the canonical SharedInfo skill in its owning repository; do not rewrite closed evidence to look current.
