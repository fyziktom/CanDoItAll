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
- `candoitall-csharp-architecture-bundle-guard`
- `csharp-architecture-governor`
- `csharp-modular-refactoring`
- `csharp-project-boundary-extraction`
- `csharp-factory-builder-composition`
- `csharp-provider-tool-plugin-isolation`
- `csharp-testability-contracts`
- `csharp-dependency-graph-audit`
- `csharp-design-pattern-selection`
- `csharp-architecture-review-gate`
- `canonical-model-review`
- `feature-block-architecture-review`
- `architecture-drift-audit`

It also depends on these public sibling skills:

- From `openai/skills`: `openai-docs`, `playwright`, `screenshot`, `imagegen`
- From `dotnet/skills`: `mtp-hot-reload`

The bundle skills also use `frontend-skill` when it is already available in the local Codex home, but the current upstream `openai/skills` cache no longer exposes that skill under this installer path.

## Install Or Refresh Skills

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1
```

That script:

- copies the custom CanDoItAll skills from this repo into `$CODEX_HOME\skills`
- copies repo-owned skill support folders such as `_csharp-architecture-shared` into `$CODEX_HOME\skills`
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

- The bundle workflow skills are tuned for ChatGPT-5.5 style operation: outcome-first, concise phase decisions, durable bundle state after compaction or resume, and evidence-driven gates.
- The bundle skills now assume real browser validation through Playwright MCP plus the `playwright` skill for UI-heavy work.
- The bundle skill pack now includes explicit readiness, subbundle-gate, and final-closure validators.
- `openai-docs` is installed with the repo skill pack so model and prompt guidance can be refreshed from official OpenAI docs on other machines.
- `candoitall-components-mcp` is the repo skill to use before inventing page-local structure in BaseLib or CanvasLib consumers. It expects the `candoitall_components` MCP server to be available and points Codex toward shared component parameters, sandbox routes, and real product usages first.
- `candoitall-codeanalytics-mcp` is the default repo skill for read-only C# investigation. It expects the `candoitall_codeanalytics` MCP server to be available and steers Codex toward scoped snapshots, dashboard health, solution/project inventory, dependency and cycle analysis, findings, DI, persistence, exact symbol tools, references, implementations, file inspection, exports, and focused context. SharpTools is backup-only and should stay disabled unless CodeAnalytics has a real unresolved capability gap.
- The C# architecture skills add a strict architecture gate for large-class refactoring, partial-class clusters, provider/tool/plugin isolation, memory protocols, process drivers, runtime composition, project references, factories, builders, catalogs, and testability work. Architecture-heavy bundles must include current-state inventory, target boundary map, dependency-direction proof, pattern selection records, testability plan, partial-class policy, and architecture checkpoints before implementation.
- `codex/csharp-architecture` keeps the package-level examples, checklists, bundle templates, and integration notes outside the discoverable skill folders. Use those artifacts when preparing architecture-heavy bundles or updating the bundle skills.
- Large-screen validation comes first: maximize the browser window or fill the available desktop work area, capture a screenshot, review it, then continue to narrower widths.
- `imagegen` is a planning aid only when UI direction is unclear; it does not replace shipped browser proof.
- The repo also ships architecture review helper docs in `codex/architecture-review` and optional repo-local custom agents in `.codex/agents`.

## Repo Plugin

- `plugins/candoitall-components-mcp` is a repo-local Codex plugin that attaches the CanDoItAll components MCP from source for plugin-based workflows.
