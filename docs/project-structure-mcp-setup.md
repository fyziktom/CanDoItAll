# Project Structure MCP Setup

## Purpose

Use `CanDoItAll.Mcp.ProjectStructure` on each Codex workstation as a thin stdio MCP client against the main CanDoItAll web instance. The MCP never talks to the local database or managed files directly.

## Central machine preparation

1. Open `/settings` in CanDoItAll web.
2. Go to the `Project Structure MCP` section.
3. Set the central base URL to the address reachable from the other workstations.
4. Create or update an agent profile with the capabilities and approval thresholds you want.
5. Save the profile and copy the generated install command or local settings JSON from the setup guide.

## Workstation install

Run the generated command from the repo root on the workstation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Install-CanDoItAllProjectStructureMcp.ps1 -RepoRoot . -ServerBaseUrl 'http://main-machine:7271' -AgentToken 'replace-with-generated-token'
```

The script does all of the following:

- publishes `CanDoItAll.Mcp.ProjectStructure` into a versioned folder under `.artifacts\mcp-installs\CanDoItAll.Mcp.ProjectStructure\`
- writes `CanDoItAll.Mcp.ProjectStructure.settings.local.json`
- repoints `.vscode\mcp.json` to the newly published entrypoint
- repoints `%USERPROFILE%\.codex\config.toml` to the newly published entrypoint

## Manual settings file

If you need to write the settings file manually, start from [CanDoItAll.Mcp.ProjectStructure.settings.example.json](/C:/repositories/CanDoItAll/CanDoItAll.Mcp.ProjectStructure.settings.example.json) and save the workstation copy as `CanDoItAll.Mcp.ProjectStructure.settings.local.json`.

## Reinstall and refresh

- Run `.\tools\Install-CanDoItAllProjectStructureMcp.ps1` again after a token rotation or base-URL change.
- Run `.\tools\Reinstall-CanDoItAllMcps.ps1` when you want the full repo MCP suite refreshed in one pass.
- Reinstall no longer needs to overwrite the currently running MCP binary, so a fresh publish can be prepared even while an older session is still connected.
- The settings UI remains the source of truth for the current token and setup command.

## Safety rules

- Keep the generated token only on trusted workstations.
- Rotate the token in CanDoItAll web when a machine is retired or the token leaks.
- Assets are readonly through the MCP. New versions must be created as revision nodes under the original asset node.
