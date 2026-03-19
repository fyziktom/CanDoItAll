# CanDoItAll

CanDoItAll is a modular .NET 10 Blazor Web App for managing project delivery work in one local workspace: projects, resources, prompts, validation runs, test evidence, workbench tabs, activity history, automation visibility, and a development-sidecar manager for watch readiness, capsule coverage, and tuning requests.

## Requirements

- .NET 10 SDK
- Windows PowerShell for the Playwright browser install script

## Run the Web App

From the repo root:

```powershell
dotnet run --project src/CanDoItAll.Web
```

Notes:

- The app uses Interactive Server rendering.
- In development it exposes the runtime readiness probe at `/_dev/runtime`.
- The default SQLite database and workspace storage live under [src/CanDoItAll.Web/.artifacts/workspace](C:/repositories/CanDoItAll/src/CanDoItAll.Web/.artifacts/workspace).

## Run the Development Manager

From the repo root:

```powershell
dotnet run --project tools/CanDoItAll.Manager
```

Notes:

- The manager listens on `http://127.0.0.1:6407` by default.
- It supervises `dotnet watch` for the web app, starts it with the web app `https` launch profile, confirms readiness through `/_dev/runtime`, and exposes loopback-only watch, capsule, and tuning endpoints.
- Capsule artifacts are written under `.artifacts/codex-capsules` at the repo root.

## Test Commands

Run the full build:

```powershell
dotnet build CanDoItAll.slnx
```

Run the test layers individually:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj
```

Playwright needs Chromium installed once per machine:

```powershell
powershell -ExecutionPolicy Bypass -File tests\CanDoItAll.Tests.Playwright\bin\Debug\net10.0\playwright.ps1 install chromium
```

## Local Data and Restore

- Workbench tab state is stored in browser local storage under `candoitall.workbench.session`.
- The web app auto-creates its SQLite database on startup.
- Search, activity, validation, test lab, and prompt factory data are persisted through the shared app database.

## Configuration

Useful defaults:

- Web app development manager base URL: `http://127.0.0.1:6407`
- Development tuning mode is enabled in [src/CanDoItAll.Web/appsettings.Development.json](C:/repositories/CanDoItAll/src/CanDoItAll.Web/appsettings.Development.json)
- Database provider defaults to SQLite unless `Database:Provider` and `Database:ConnectionString` are overridden

## Current Scope

The repo is internally beta-ready for the architecture package scope:

- Workspace, providers, projects, resources, prompt gallery, prompt factory, validation center, test lab, activity, automation, and workbench surfaces are implemented.
- The local manager covers watch supervision, readiness confirmation, capsule coverage, and development-only tuning requests.
- Unit, integration, component, and Playwright smoke tests are in place.
