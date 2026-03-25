# Validation Checklist

Run these checks after resetup or repo onboarding.

## Machine checks

- `%USERPROFILE%\.codex\config.toml` has a `candoitall_dotnetwatch` section that points at `tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1`
- the global config includes the intended `-Configuration` argument
- `CanDoItAll\.vscode\mcp.json` points at the same wrapper-backed command
- `%USERPROFILE%\.codex\skills\candoitall-dotnetwatch-setup\SKILL.md` exists
- `%USERPROFILE%\.codex\skills\candoitall-watch-playwright-loop\SKILL.md` exists
- the startup shortcut exists
- the desktop shortcut exists

## Repo checks

- no repo-specific `*_dotnetwatch` MCP entry remains
- no stale repo-specific dotnetwatch settings file remains unless it is still intentionally used
- repo instructions explicitly say to use the shared `candoitall_dotnetwatch`

## Functional checks

- the tray shortcut launches without path errors
- the shared backend can start the intended project through `candoitall_app_start` or the manager project picker
- for UI work, the repo instructions point Codex to `candoitall-watch-playwright-loop`
- if resetup was run from the current Codex session and direct MCP calls now report `Transport closed`, reopen the Codex session before treating that as a failed install
