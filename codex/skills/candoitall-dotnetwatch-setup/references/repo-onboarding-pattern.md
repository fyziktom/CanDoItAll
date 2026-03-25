# Repo Onboarding Pattern

Use this when another repo should be developed through the shared CanDoItAll backend.

## Architecture rule

- The machine owns one shared `candoitall_dotnetwatch` MCP.
- That MCP is anchored to the CanDoItAll repo and settings.
- Other repos are started by passing their project path to `candoitall_app_start` or by using the backend manager project picker.
- Do not create `<repo>_dotnetwatch` in `%USERPROFILE%\.codex\config.toml` or in repo-local `.vscode\mcp.json`.

## Start pattern for another repo

Resolve the target repo root first, then start the app explicitly:

```text
projectPath: <repo-root>\src\MyApp\MyApp.csproj
workingDirectory: <repo-root>\src\MyApp
launchProfile: https
reuseIfCompatible: true
```

Add `urls`, `configuration`, or environment overrides only when the project actually needs them.

## Repo cleanup checklist

- remove repo-specific dotnetwatch settings files that are no longer used
- remove repo-local MCP config entries that duplicate `candoitall_dotnetwatch`
- rewrite repo instructions so they describe the shared backend pattern
- mention the resetup skill when the machine-level install is missing or stale

## External repo support

The default CanDoItAll settings allow sibling repos through `Security.AllowedExternalProjectRoots`. Keep other projects beside the CanDoItAll repo unless the settings were intentionally tightened.
