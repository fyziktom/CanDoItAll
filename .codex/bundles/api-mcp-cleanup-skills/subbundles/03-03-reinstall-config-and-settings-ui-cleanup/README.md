# 03 Reinstall Config And Settings UI Cleanup

## Status

- `Completed`

## Objective

Remove ProjectStructure and Processes MCP install/config generation and remove MCP-specific Settings UI.

## Covered Inputs

- Original request items 3 and 4.
- R-004 and R-005.

## Prerequisites

- Subbundle 02 closed.

## Exact Source References

- C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1
- C:\repositories\CanDoItAll\.vscode\mcp.json
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor.cs
- C:\Users\lucys\.codex\config.toml

## Removed Targets

- `tools/Install-CanDoItAllProjectStructureMcp.ps1`
- `tools/Install-CanDoItAllProcessesMcp.ps1`

## Deliverables

- Reinstall script publishes/configures only remaining MCPs and still syncs skills.
- Dedicated install scripts for removed MCPs are deleted.
- VS Code and local Codex MCP config no longer include removed entries.
- Settings page no longer exposes the Project Structure MCP tab/panel.

## Dependency Impact

- API skills install depends on reinstall script preserving skill sync.
- UI tests may need removal/update after the tab disappears.

## Validation Depth

- Source search, config inspection, component/build proof, and browser proof if launchable.

## Implementation Steps

1. Remove removed MCP publish/config/manifest entries from reinstall script.
2. Delete dedicated install scripts and removed MCP settings files.
3. Update local `config.toml` and repo `.vscode\mcp.json`.
4. Remove Settings tab/panel and stale component tests.

## Do Not Do

- Do not remove API Access JWT settings.
- Do not remove remaining MCP entries.

## Acceptance Checklist

- Reinstall script cannot regenerate the removed MCP config sections.
- Settings UI no longer contains Project Structure MCP text.
- Local Codex config no longer contains `candoitall_projectstructure` or `candoitall_processes`.

## Proof Required

- Source/config search output in execution report.
- Settings UI proof or a documented launch blocker.

## Closure Proof

- Removed ProjectStructure/Processes MCP publishing, VS Code config generation, dedicated install scripts, and settings files.
- `tools/Reinstall-CanDoItAllMcps.ps1` now removes stale local TOML sections for the deleted MCPs instead of regenerating them.
- Removed Settings page Project Structure MCP tab/panel and associated administration services.
- Local `C:\Users\lucys\.codex\config.toml` contains no removed MCP sections.
- Browser proof was not captured; source search plus build proof is recorded in the execution report.

## Browser Validation Logging

- If the app runs, capture `/settings` proof that the MCP tab is gone.

## Progression Gate

- Skills may be installed after the settings/config cleanup cannot regenerate removed MCP entries.

## Suggested Agent Prompt

Clean only the removed MCP install/config/UI surfaces. Preserve remaining MCPs and the API Access JWT tab.
