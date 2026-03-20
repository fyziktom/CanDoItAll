# Sample environment policy

## Default environment values for child dotnet processes

| Key | Value | Notes |
|---|---|---|
| `DOTNET_CLI_UI_LANGUAGE` | `en` | Use English CLI output for more deterministic parsing |
| `DOTNET_NOLOGO` | `1` | Reduce log noise |
| `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` | `1` | Avoid first-run churn |
| `DOTNET_WATCH_RESTART_ON_RUDE_EDIT` | `1` | Do not wait for interactive input |
| `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER` | `1` | Browser control belongs to the client/browser tool |
| `DOTNET_WATCH_SUPPRESS_EMOJIS` | `1` | Reduce parsing noise |

## Optional environment values

| Key | Value | When to use |
|---|---|---|
| `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH` | `1` | If explicit browser refresh is preferred |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `1` | If file system notifications are unreliable |
