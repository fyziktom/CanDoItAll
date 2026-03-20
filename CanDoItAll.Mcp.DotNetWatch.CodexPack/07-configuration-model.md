# Configuration model

## 1. Cíl konfigurace

Konfigurace musí:
- být dostatečně explicitní pro CanDoItAll,
- fail-fast validovat chyby při startu,
- dovolit bezpečný override bez otevření plně generického shell runneru,
- být čitelná pro člověka i Codex.

Doporučený model:
- silně typované options classes
- binding z JSON + env + volitelně command line
- centrální validátor
- redacted config snapshot pro `workspace_info`

## 2. Doporučený soubor

Název:
- `CanDoItAll.Mcp.DotNetWatch.settings.json`

Alternativně:
- standardní `appsettings.json` + `appsettings.Local.json`

Pro lokální workflow je přehlednější oddělený settings soubor.

## 3. Doporučená struktura

```json
{
  "Server": {
    "Name": "CanDoItAll.Mcp.DotNetWatch",
    "WorkspaceRoot": ".",
    "SolutionPath": "CanDoItAll.sln"
  },
  "DefaultApp": {
    "ProjectPath": "src/CanDoItAll.Web/CanDoItAll.Web.csproj",
    "WorkingDirectory": "src/CanDoItAll.Web",
    "Mode": "WatchRun",
    "Configuration": "Debug",
    "Framework": null,
    "LaunchProfile": null,
    "Arguments": [],
    "Urls": [
      "https://localhost:7010",
      "http://localhost:5010"
    ],
    "EnvironmentOverlay": {
      "ASPNETCORE_ENVIRONMENT": "Development"
    }
  },
  "Health": {
    "Enabled": true,
    "Urls": [
      "https://localhost:7010/health"
    ],
    "TimeoutMs": 2000,
    "PollIntervalMs": 500,
    "StableSuccessCount": 2,
    "AcceptInsecureLocalhostHttps": true,
    "AllowedHosts": [
      "localhost",
      "127.0.0.1",
      "::1"
    ]
  },
  "Build": {
    "DefaultTargetPath": "CanDoItAll.sln",
    "DefaultWhenAppRunning": "StopAndResume",
    "DefaultTimeoutMs": 1800000,
    "ExtraArguments": []
  },
  "Tests": {
    "DefaultTargetPath": "CanDoItAll.sln",
    "DefaultWhenAppRunning": "StopAndResume",
    "DefaultTimeoutMs": 1800000,
    "RunnerPreference": "Auto",
    "DefaultFilter": null,
    "Projects": []
  },
  "Logs": {
    "BufferCapacity": 5000,
    "PersistToFile": true,
    "Folder": ".mcp-state/logs",
    "MaxFileSizeMb": 50,
    "RedactionEnabled": true,
    "IncludeSystemEvents": true
  },
  "Process": {
    "GracefulStopTimeoutMs": 5000,
    "ForceKillAfterMs": 10000,
    "CleanupStaleManagedProcessesOnStartup": true,
    "RegistryPath": ".mcp-state/process-registry.json",
    "UsePollingFileWatcher": false
  },
  "Waits": {
    "DefaultAppWaitTimeoutMs": 120000,
    "DefaultOperationWaitTimeoutMs": 1800000,
    "DefaultPollIntervalMs": 500,
    "DefaultQuietPeriodMs": 2000
  },
  "Security": {
    "AllowedProjectRoots": [
      "src",
      "tests"
    ],
    "AllowExternalHealthHosts": false,
    "AllowedEnvironmentKeys": [
      "ASPNETCORE_ENVIRONMENT",
      "ASPNETCORE_URLS",
      "DOTNET_ENVIRONMENT",
      "DOTNET_USE_POLLING_FILE_WATCHER"
    ]
  }
}
```

## 4. Doporučené options classes

- `McpServerOptions`
- `ServerOptions`
- `DefaultAppOptions`
- `HealthOptions`
- `BuildOptions`
- `TestOptions`
- `LogOptions`
- `ProcessOptions`
- `WaitOptions`
- `SecurityOptions`

## 5. Detailní popis sekcí

### 5.1 `Server`
| Klíč | Typ | Povinné | Popis |
|---|---|---:|---|
| `Name` | string | ano | Jméno serveru pro logging a metadata |
| `WorkspaceRoot` | string | ano | Kořen repozitáře |
| `SolutionPath` | string | ano | Cesta na `.sln` |

Validace:
- `WorkspaceRoot` musí existovat.
- `SolutionPath` musí existovat a ležet uvnitř workspace.

### 5.2 `DefaultApp`
| Klíč | Typ | Povinné | Popis |
|---|---|---:|---|
| `ProjectPath` | string | ano | Výchozí startup projekt |
| `WorkingDirectory` | string | ne | Default workdir; když chybí, použije se složka projektu |
| `Mode` | enum | ano | `WatchRun` nebo `RunOnce` |
| `Configuration` | string | ano | Typicky `Debug` |
| `Framework` | string/null | ne | Např. `net10.0` |
| `LaunchProfile` | string/null | ne | Launch profile |
| `Arguments` | array | ne | Default app args |
| `Urls` | array | ne | Explicitní URL override |
| `EnvironmentOverlay` | object | ne | Whitelistované env klíče |

Validace:
- `ProjectPath` musí existovat.
- projekt musí být pod povoleným rootem.
- `Mode` musí být známá hodnota.

### 5.3 `Health`
| Klíč | Typ | Povinné | Popis |
|---|---|---:|---|
| `Enabled` | bool | ano | Zapne health probing |
| `Urls` | array | podmíněně | Health URL seznam |
| `TimeoutMs` | int | ano | Timeout jednoho HTTP requestu |
| `PollIntervalMs` | int | ano | Poll interval |
| `StableSuccessCount` | int | ano | Kolik po sobě jdoucích success je potřeba |
| `AcceptInsecureLocalhostHttps` | bool | ano | Povolit localhost self-signed HTTPS |
| `AllowedHosts` | array | ano | Whitelist hostů |

Validace:
- pokud `Enabled=true`, musí být alespoň jedna URL nebo generovatelná URL.
- host každé URL musí být v whitelistu, pokud není výslovně povoleno jinak.

### 5.4 `Build` a `Tests`
Obě sekce definují:
- default target
- default policy při běžící app
- timeout
- default extra args

`Tests` navíc:
- `RunnerPreference`
- `DefaultFilter`
- `Projects`

### 5.5 `Logs`
`BufferCapacity`:
- počet entry v in-memory ring bufferu

`PersistToFile`:
- jestli ukládat logy i do souborů

`Folder`:
- doporučeně `.mcp-state/logs`

Validace:
- cesta musí být zapisovatelná
- složka musí být mimo watch-sensitive path nebo explicitně excluded z watch

### 5.6 `Process`
`GracefulStopTimeoutMs` a `ForceKillAfterMs` musí být kladné a rozumné.  
Doporučení:
- `ForceKillAfterMs >= GracefulStopTimeoutMs`

`RegistryPath`:
- soubor pro stale process registry

`UsePollingFileWatcher`:
- fallback pro specifická prostředí; pokud true, server může přidat `DOTNET_USE_POLLING_FILE_WATCHER=1`

### 5.7 `Waits`
Defaulty pro wait tooly.

### 5.8 `Security`
`AllowedProjectRoots`:
- relativní rooty pod workspace, odkud lze spouštět projekty

`AllowedEnvironmentKeys`:
- whitelist env klíčů, které smí jít z requestu do child procesu

## 6. Fail-fast validační pravidla

Při startu serveru validuj minimálně:

1. existence workspace root
2. existence solution path
3. existence default app project
4. rozumné timeouty
5. `ForceKillAfterMs >= GracefulStopTimeoutMs`
6. log folder existuje nebo jde vytvořit
7. registry path je zapisovatelná
8. health URLs mají povolené hosty
9. `AllowedProjectRoots` jsou uvnitř workspace
10. `AllowedEnvironmentKeys` neobsahují wildcards typu `*`

## 7. Defaultní environment overlay

Na child `dotnet` procesy doporučuji aplikovat tyto defaulty:

| Klíč | Hodnota | Proč |
|---|---|---|
| `DOTNET_CLI_UI_LANGUAGE` | `en` | determinističtější parsování logů a chyb |
| `DOTNET_NOLOGO` | `1` | méně šumu v logu |
| `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` | `1` | stabilnější CLI startup |
| `DOTNET_WATCH_RESTART_ON_RUDE_EDIT` | `1` | non-interactive režim nesmí čekat na vstup |
| `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER` | `1` | browser start má řídit klient |
| `DOTNET_WATCH_SUPPRESS_EMOJIS` | `1` | čistší parsování watch logů |

Doplňkově a konfigurovatelně:
- `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1`
- `DOTNET_USE_POLLING_FILE_WATCHER=1`

## 8. Konfigurační precedence

Doporučené pořadí:

1. hardcoded safe defaults
2. settings JSON
3. environment variables
4. explicit tool request override

Ale:
- tool request override smí přepsat jen whitelistované hodnoty,
- ne bezpečnostní omezení.

## 9. Redacted config snapshot

`workspace_info(includeConfigSnapshot=true)` může vrátit snapshot konfigurace, ale:
- bez secret-like env hodnot,
- bez connection stringů,
- bez ne-whitelistovaných env klíčů,
- s maskováním citlivých suffixů.

Příklad:
```json
{
  "environmentOverlay": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "CUSTOM_API_KEY": "***redacted***"
  }
}
```

## 10. Doporučené watch exclusions

V `.csproj` nebo společné props vrstvě doporučuji vyloučit minimálně:

- `.mcp-state/**`
- `playwright-report/**`
- `test-results/**`
- `TestResults/**`
- `artifacts/**`
- `coverage/**`
- `screenshots/**`

Viz také:
- `15-examples/watch-exclusions.snippet.xml`

## 11. Co je potřeba doplnit při repo discovery

Konkrétně zjistit:
- skutečný startup project path
- skutečný health endpoint
- seznam test projektů
- zda repo používá `Directory.Packages.props`
- zda některé environment klíče musí být whitelistem povoleny

## 12. Doporučení pro implementaci bindingu

- bind do jednoho root options objektu
- použij `IValidateOptions<T>`
- validuj i normalizované absolutní cesty
- po validaci vytvoř immutable runtime snapshot
- tooly pracují s runtime snapshotem, ne s volně mutovatelným config objektem
