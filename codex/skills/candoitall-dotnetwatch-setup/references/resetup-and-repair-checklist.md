# Resetup And Repair Checklist

Use this when the shared backend is missing, stale, duplicated, or the machine was previously wired to repo-specific dotnetwatch MCP entries.

## Standard resetup

Run from the CanDoItAll repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Reinstall-CanDoItAllMcps.ps1 -McpRepoRoot ..\CanDoItAll.Mcp
```

That script is the supported machine repair path. It:

- prepares the current shadow host through the sibling `CanDoItAll.Mcp` wrapper
- republishes the managed MCP companions from the sibling `CanDoItAll.Mcp` repo into `.artifacts\mcp-installs`
- syncs repo-managed skills into `%USERPROFILE%\.codex\skills`
- updates `CanDoItAll\.vscode\mcp.json`
- updates `%USERPROFILE%\.codex\config.toml`
- refreshes the tray startup shortcut
- refreshes the tray desktop shortcut
- removes stale backend catalog records for the current workspace
- stops the published companion executables before republishing them so shortcut-launched tray instances do not lock the install folder

## Active-session caveat

If Codex runs the resetup from a session that is already attached to the old MCP transport, the current thread may still see `Transport closed` until the MCP client reattaches. Treat that as a post-resetup session issue, not as proof that the reinstall failed.

When that happens:

- validate the rewritten files and shortcuts first
- launch the tray app from the refreshed shortcut if you need the operator surface immediately
- reopen or restart the Codex session before judging the repaired MCP transport

## When to rerun it

- the MCP wrapper or backend source changed
- `%USERPROFILE%\.codex\config.toml` points to stale paths
- the tray shortcut is missing
- the desktop shortcut is missing
- another repo was given its own dotnetwatch MCP entry and setup needs to be normalized
- the machine was reinstalled or CanDoItAll moved to a new path

## Targeted repair rules

- If only repo guidance is wrong, fix the repo instructions and remove stale repo-specific config; do not invent a second MCP.
- If only the user config is stale, rerun the full resetup anyway unless the user explicitly asks for a surgical edit.
- If duplicate backends appear, validate the wrapper-backed shared install before debugging repo code.

## Supported script parameters

- `-RepoRoot <path>` when CanDoItAll is not in the current directory
- `-McpRepoRoot <path>` when the MCP repo is not the sibling `CanDoItAll.Mcp` directory
- `-UserConfigPath <path>` when Codex config lives outside `%USERPROFILE%\.codex\config.toml`
- `-ShadowConfiguration Release|Debug` when testing the wrapper host configuration
- `-SkipProcessReset` skips the broader watch/backend reset, but the script may still stop published companion executables that would otherwise lock the install folders
- `-SkipSkillSync`, `-SkipUserConfig`, `-SkipVsCodeConfig`, `-SkipTrayStartupShortcut`, `-SkipTrayDesktopShortcut` only for controlled repair scenarios
