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
- `candoitall-frontend-theme`

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
- Large-screen validation comes first: maximize the browser window or fill the available desktop work area, capture a screenshot, review it, then continue to narrower widths.
- `imagegen` is a planning aid only when UI direction is unclear; it does not replace shipped browser proof.
