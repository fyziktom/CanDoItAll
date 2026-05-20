# CanDoItAll

CanDoItAll is a local-first .NET 10 Blazor Web App for project delivery work. It combines project structure, process templates and runs, workbench views, prompts, resources, validation, test evidence, activity, automation, CRM/HR staffing, AgentFramework-backed AI agents, and an HTTP API control plane in one workspace.

The current architecture is modular: `CanDoItAll.Web` hosts the app and HTTP API, `CanDoItAll.Composition` wires the runtime, `CanDoItAll.Infrastructure` owns data/storage/control-plane concerns, `CanDoItAll.Modules.*` own product behavior, `CanDoItAll.AgentFramework.*` owns technical agent execution, and `CanDoItAll.Mcp.*` exposes selected local-development sidecars. Process, project-structure, and agent operations should use the HTTP APIs and repo-managed Codex skills; the old Processes and ProjectStructure MCP servers are suppressed for now.

## Overview

```mermaid
flowchart LR
    User[Local user] --> Web[CanDoItAll.Web Blazor host]
    Agent[AI coding agent] --> Api[HTTP API control plane]
    Agent --> Mcp[Local MCP sidecars]

    Api --> Web
    Web --> Composition[CanDoItAll.Composition]
    Composition --> Infrastructure[Infrastructure and control plane]
    Composition --> Modules[Runtime modules]
    Modules --> Processes[Processes runtime]
    Modules --> Workbench[Projects, Workbench, Workspace]
    Modules --> AgentModule[AgentFramework module]
    Modules --> CrmHr[CRM/HR staffing]

    Processes --> AgentModule
    AgentModule --> AgentFramework[AgentFramework Core and MAF runtime]
    AgentFramework --> Providers[AI providers]
    AgentFramework --> Tools[Workspace, process, project-structure, skill, API, and MCP tools]

    Infrastructure --> AppDb[(Active AppDbContext profile)]
    Infrastructure --> ControlPlane[(Control-plane files)]
    AgentFramework --> WorkspaceFiles[(File sandbox workspace)]
    Processes --> ManagedFiles[(Managed artifacts)]

    Mcp --> DevLoop[Code analytics, components, dotnet watch, Mermaid, SSH]
    DevLoop --> Infrastructure
```

## Architecture Docs

- [Architecture beta](docs/architecture-beta.md): detailed current architecture with GitHub-safe Mermaid flowcharts, C4, class, and sequence diagrams, including process execution with AI agents.
- [Docs index](docs/README.md): repository documentation map.
- [Cognitive Memory](docs/cognitive-memory/README.md): current implementation stage, architecture, API, validation, and roadmap for Cognitive Memory.
- [Enterprise operating system](docs/enterprise-operating-system.md): customer-facing explanation of CanDoItAll as an operating system for projects.
- [API control plane](docs/api-control-plane.md): current process, project-structure, project, and agent HTTP APIs.
- [Architecture index](architecture/README.md): current architecture docs, ADRs, and historical architecture reviews.
- [Shared components](docs/ui-shared-components/README.md): current shared component-library split and usage guidance.

## Requirements

- .NET 10 SDK
- Windows PowerShell for local install scripts and Playwright browser install
- Node.js and npm when rebuilding the shared Tailwind output
- `git` when installing or refreshing the portable Codex skill pack
- Docker Desktop or another Docker-compatible runtime when using the repo-managed PostgreSQL and Qdrant containers
- PostgreSQL on `127.0.0.1:5432` for the default Development/Visual Studio profile
- Qdrant on `localhost:6334` when validating Cognitive Memory vector projection or recall

## Quick Start With PostgreSQL And Qdrant

From a clean clone, start the default local services:

```powershell
docker compose up -d postgres qdrant
```

The compose file starts:

- PostgreSQL `postgres:16-alpine` on `127.0.0.1:5432` with database `candoitall_development`, user `candoitall`, and password `candoitall`.
- Qdrant `qdrant/qdrant:v1.15.3` on HTTP port `6333` and gRPC port `6334`.

Run the web app after the containers are healthy:

From the repo root:

```powershell
dotnet run --project src/CanDoItAll.Web
```

The Development and Visual Studio `http`/`https` launch profiles point at:

```text
Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true
```

If you use a native PostgreSQL service instead of Docker, prepare the same role and database with:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dev\Ensure-DevelopmentPostgres.ps1
```

Runtime notes:

- The app uses Blazor Interactive Server rendering.
- The default Development and Visual Studio `http`/`https` profiles use PostgreSQL database `candoitall_development` with username/password `candoitall`/`candoitall`.
- Qdrant is configured in `src/CanDoItAll.Web/appsettings.json` under `Rag:Qdrant` with gRPC port `6334`, collection `candoitall-knowledge`, vector size `384`, and create-collection-if-missing enabled.
- Development control-plane and workspace files are rooted under `%LOCALAPPDATA%\CanDoItAll`, not repo `.artifacts`, so a clean clone can start without carrying local artifact settings.
- Development readiness is exposed at `/_dev/runtime`.
- Development database selection is exposed at `/_dev/database/selection`.
- If no explicit `Database:Provider` and `Database:ConnectionString` override is supplied, the app resolves its active database through the control plane rooted at `%LOCALAPPDATA%\CanDoItAll\control-plane` by default.
- SQLite profiles still exist for now, but they are no longer the default development path. They are likely to be removed after more analysis because governed process runs are too slow on SQLite for this runtime.

See [Development runtime](docs/development-runtime.md) for PostgreSQL/Qdrant setup details and troubleshooting.

## Install And Local Tooling Scripts

Use these scripts from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Install-CanDoItAllWebApp.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Reinstall-CanDoItAllMcps.ps1
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1
```

What they do:

- `tools\Install-CanDoItAllWebApp.ps1` publishes `CanDoItAll.Web` as a self-contained Windows app under `%LOCALAPPDATA%\CanDoItAll\WebApp` by default, creates `Start-CanDoItAll.ps1`, creates a desktop shortcut, and can launch the app with `-StartAfterInstall`.
- `tools\Reinstall-CanDoItAllMcps.ps1` rebuilds and installs the repo-managed Components, CodeAnalytics, and SshOps MCP sidecars plus companion tools under `.artifacts\mcp-installs`, prepares the DotNetWatch shadow artifact, updates VS Code and Codex MCP configuration, creates DotNetWatch tray shortcuts, and removes stale `candoitall_processes` and `candoitall_projectstructure` config sections.
- `codex\scripts\install-candoitall-skills.ps1` installs repo-managed CanDoItAll skills into `$CODEX_HOME\skills` and installs required public sibling skills from `openai/skills` and `dotnet/skills`.

Current active MCP sidecars are CodeAnalytics, Components, DotNetWatch, Mermaid, SshOps, and LocalRuntime helpers. Processes and ProjectStructure MCP servers are not active; use the HTTP API control plane and repo-managed `candoitall-api-*` skills for those surfaces.

## Run The Development Manager

From the repo root:

```powershell
dotnet run --project tools/CanDoItAll.Manager
```

The manager listens on `http://127.0.0.1:6407` by default. It supervises `dotnet watch` for the web app, confirms readiness through `/_dev/runtime`, exposes loopback-only watch/capsule/tuning endpoints, and writes capsule artifacts under `.artifacts/codex-capsules`.

## Test Commands

Run the full build:

```powershell
dotnet build CanDoItAll.slnx
```

Run the main test layers individually:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj
```

Install Chromium for Playwright once per machine after the Playwright test project is built:

```powershell
powershell -ExecutionPolicy Bypass -File tests\CanDoItAll.Tests.Playwright\bin\Debug\net10.0\playwright.ps1 install chromium
```

## Project Families

| Family | Responsibility |
| --- | --- |
| `CanDoItAll.Web`, `CanDoItAll.Composition`, `CanDoItAll.Infrastructure`, `CanDoItAll.SharedKernel` | Host, composition, data/control-plane/storage/readiness, shared primitives. |
| `CanDoItAll.Modules.*` | Product modules for projects, processes, workbench, workspace, prompts, resources, validation, automation, CRM/HR, AgentFramework, activity, collaboration, security, test lab, Scheduler Planner, Plugins, and Cognitive Memory. |
| `CanDoItAll.AgentFramework.*` | Technical agent catalog, provider profiles, file-backed workspaces, Microsoft Agent Framework runtime adapter, workspace tools, MCP capabilities, execution runs, artifacts, UI components, and voice capture/synthesis services. |
| `CanDoItAll.Components.*` | Shared Razor UI primitives, charts, Mermaid diagrams, canvas controls, overlay windows, WebGL workbench runtime, facade, and sandbox projects. |
| `src/plugins/*` and `CanDoItAll.Plugins.Abstractions` | Bundled plugin contracts and implementations for Docker, Gmail, Office 365, and shared email workflow payloads. |
| `CanDoItAll.Mcp.*` | Agent-facing sidecars for code analytics, components, dotnet watch, Mermaid, SSH operations, and local runtime helpers. Processes and ProjectStructure MCPs are not active; use HTTP APIs for those surfaces. |
| `CanDoItAll.Migrations.*` | Provider-specific EF Core migrations for SQLite and PostgreSQL. |
| `tests/*` | Unit, integration, component, Playwright, support, and MCP-focused tests. |
| `tools/*` | Local manager, dotnet-watch tray, MCP harness, RPI validation artifacts, scenario seeding tools, and install/dev scripts. |

Each tracked `.csproj` directory under `src`, `tests`, and `tools` has a local `README.md` with the project purpose, references, and validation notes.

## Process Runtime And AI Agents

Processes are durable runtime workflows. A process run materializes assignments, step runs, work briefs, artifact expectations, dependencies, decisions, journals, and outbox work. Ready steps are dispatched by `ProcessRunAutomationDispatchService`, which resolves the assigned CRM/HR AI party to a technical AgentFramework agent, builds a governed process-step prompt, executes the agent, audits required tool and evidence use, projects execution artifacts into managed storage, records process artifacts, and transitions the step.

Agent execution is handled by `CanDoItAll.AgentFramework.*`. The Microsoft Agent Framework adapter attaches permitted workspace, process, project-structure, skill, API, MCP, and provider-native tools. Execution state, chat sessions, tool receipts, artifacts, approvals, and metrics are persisted in the active profile's file sandbox workspace.

Real process-agent automation should run against a PostgreSQL AppDbContext profile when `Processes:Runtime:RequirePostgreSqlForAgentAutomation` is enabled. SQLite remains useful for local module smoke work, but governed multi-agent process runs are expected to use PostgreSQL so step journals, tool receipts, artifacts, and recovery attempts do not bottleneck the run.

The managed OpenAI agent/provider seed defaults to `gpt-5-mini`. The runtime omits temperature for OpenAI-style models that only accept the provider default temperature and retries without temperature when the provider reports an unsupported temperature parameter.

## Local Data

- App data lives in the active `AppDbContext` profile.
- Control-plane metadata and DataProtection keys live under `%LOCALAPPDATA%\CanDoItAll\control-plane` unless `ControlPlane:RootPath` is overridden.
- Managed artifacts are routed through infrastructure storage services.
- AgentFramework catalog, chat, execution, receipt, and artifact files live in a profile-scoped file sandbox workspace.
- Workbench browser tab state is stored in local storage under `candoitall.workbench.session`.

## Codex Skills, API, And MCP Setup

The portable Codex skill pack is documented in [codex/README.md](codex/README.md).

Install or refresh the CanDoItAll custom skills and required public sibling skills with:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1
```

Current API and MCP notes:

- [API control plane](docs/api-control-plane.md)
- [Processes MCP transition note](docs/processes-mcp-setup.md)
- [Project Structure MCP transition note](docs/project-structure-mcp-setup.md)
- [DotNetWatch persistent backend notes](docs/mcp-dotnetwatch-persistent-backend-benefits.md)

For full local MCP resetup, use `tools\Reinstall-CanDoItAllMcps.ps1`; it includes skill sync unless `-SkipSkillSync` is supplied.

## Styling

Shared component styling is built through the Tailwind workspace:

```powershell
npm install
npm run tailwind:build
```

The output is written to `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.
