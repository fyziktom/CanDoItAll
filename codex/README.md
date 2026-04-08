# Codex Skills

This repo ships a portable CanDoItAll Codex skill pack under `codex/skills`.

It includes these custom skills:

- `candoitall-bundle-workflow`
- `candoitall-bundle-preparation`
- `candoitall-bundle-execution`
- `candoitall-bundle-validator`
- `candoitall-subbundle-validator`
- `candoitall-watch-playwright-loop`
- `candoitall-dotnetwatch-setup`
- `candoitall-components-mcp`
- `candoitall-codeanalytics-mcp`
- `candoitall-frontend-theme`
- `canonical-model-review`
- `feature-block-architecture-review`
- `architecture-drift-audit`

It also depends on these public sibling skills:

- From `openai/skills`: `frontend-skill`, `playwright`, `screenshot`, `imagegen`
- From `dotnet/skills`: `mtp-hot-reload`

## Install Or Refresh Skills

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1
```

That script:

- copies the custom CanDoItAll skills from this repo into `$CODEX_HOME\skills`
- clones or updates the public `openai/skills` and `dotnet/skills` repos into temp caches
- finds the required public sibling skills by name in the correct upstream repo
- installs those sibling skills into the same Codex home

## Useful Options

Install only the custom repo-backed skills:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1 -SkipPublicSkills
```

Install into a different Codex home:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1 -CodexHome "D:\CodexHome"
```

## Notes

- The bundle skills now assume real browser validation through Playwright MCP plus the `playwright` skill for UI-heavy work.
- The bundle skill pack now includes explicit readiness, subbundle-gate, and final-closure validators.
- `candoitall-components-mcp` is the repo skill to use before inventing page-local structure in BaseLib or CanvasLib consumers. It expects the `candoitall_components` MCP server to be available and points Codex toward shared component parameters, sandbox routes, and real product usages first.
- `candoitall-codeanalytics-mcp` is the default repo skill for read-only C# investigation. It expects the `candoitall_codeanalytics` MCP server to be available and steers Codex toward solution inventory, document inspection, exact symbol tools, and focused context. SharpTools is backup-only and should stay disabled unless CodeAnalytics has a real unresolved capability gap.
- Large-screen validation comes first: maximize the browser window or fill the available desktop work area, capture a screenshot, review it, then continue to narrower widths.
- `imagegen` is a planning aid only when UI direction is unclear; it does not replace shipped browser proof.
- The repo also ships architecture review helper docs in `codex/architecture-review` and optional repo-local custom agents in `.codex/agents`.

## Repo Plugin

- `plugins/candoitall-components-mcp` is a repo-local Codex plugin that attaches the CanDoItAll components MCP from source for plugin-based workflows.
