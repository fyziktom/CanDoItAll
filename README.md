# CanDoItAll

[![CI](https://github.com/fyziktom/CanDoItAll/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/fyziktom/CanDoItAll/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

![aicandoitall](https://aicandoitall.com/assets/images/product/gallery/framework-development-paths.png)

CanDoItAll is a local-first .NET 10 Blazor application for governed project delivery,
durable process execution, workforce coordination, and AI-agent automation. Product
information is available at [aicandoitall.com](https://aicandoitall.com); this repository
focuses on the implementation and its engineering contracts.

## Ownership

This repository owns:

- the Blazor host, HTTP API, application composition, and product modules
- project, workflow, process, CRM/HR, plugin, Memory, and automation behavior
- provider-neutral AgentFramework contracts and Microsoft Agent Framework adapters
- PostgreSQL persistence, migrations, runtime templates, tests, and repository tooling

This repository does not own:

- development MCP servers from [CanDoItAll.Mcp](https://github.com/fyziktom/CanDoItAll.Mcp)
- shared Blazor components from [CanDoItAll.Components](https://github.com/fyziktom/CanDoItAll.Components)
- reusable file, browser, and desktop adapters from [CanDoItAll.FileTools](https://github.com/fyziktom/CanDoItAll.FileTools)
- family standards and reusable Codex assets from [CanDoItAll.SharedInfo](https://github.com/fyziktom/CanDoItAll.SharedInfo)
- native Cognitive Memory from [CanDoItAll.CognitiveMemory](https://github.com/fyziktom/CanDoItAll.CognitiveMemory)

## Entry Points

| Entry point | Responsibility |
|---|---|
| [`CanDoItAll.Web`](src/App/CanDoItAll.Web/README.md) | Blazor host, HTTP API, OpenAPI, and runtime endpoints |
| [`CanDoItAll.Composition`](src/App/CanDoItAll.Composition/README.md) | Dependency injection and application runtime composition |
| [`src/Modules`](src/Modules/README.md) | Product-facing bounded modules, including the AgentFramework-hosted Simple Chats experience |
| [`src/Processes`](src/Processes/README.md) | Durable process model, execution, projections, and persistence |
| [`src/Memory`](src/Memory/README.md) | Provider-neutral Memory contracts, drivers, and persistence |
| [`src/MAF`](src/MAF/README.md) | AgentFramework and Microsoft Agent Framework integration |
| [`Templates`](Templates/README.md) | Repository-owned runtime seed and template packs |

`CanDoItAll.slnx` is the canonical product solution. Test projects are intentionally
kept out of that build graph and have suite-specific entry points under
[`tests/Solutions`](tests/Solutions).

Simple Chats provides provider-neutral ordinary conversations without creating agents or agent runs.
The AgentFramework workspace hosts definition and conversation views, floating conversations,
Prompt Gallery composer actions, and combined Agent/Simple Chat usage analytics. Its asynchronous HTTP
contract remains available to remote clients: turn admission returns a durable operation, a hosted
dispatcher owns provider execution, and clients follow status or replayable SSE. See
[LLM Chats product and API](docs/llm-chats-api.md).

## Requirements

- the .NET SDK selected by [`global.json`](global.json)
- Windows, Linux, or macOS for a direct source build; supported publish targets are
  described in [Installing instances](docs/operations/installing-instances.md#choose-a-deployment-model)
- sibling `CanDoItAll.Components` and `CanDoItAll.FileTools` source repositories for the
  default local-development dependency mode, or an explicit package-mode build
- PostgreSQL 16 or a compatible supported server
- Docker Desktop or another Compose v2 runtime when using the development application stack
- Node.js and npm when rebuilding application Tailwind output
- PowerShell 7 on Windows, Linux, or macOS for repository automation; the dedicated
  Windows installer and generated launcher remain compatible with Windows PowerShell 5.1

## Build Dependency Modes

Local development uses source projects from the Components and FileTools repositories by
default. Clone all three repositories with these exact sibling names; casing matters on
case-sensitive filesystems:

```text
<parent>/
  CanDoItAll/
  CanDoItAll.Components/
  CanDoItAll.FileTools/
```

[`Directory.Build.targets`](Directory.Build.targets) replaces matching package references
with project references from those roots. A non-sibling layout must pass both root
properties to every restore, build, test, and publish command:

```powershell
dotnet restore ./CanDoItAll.slnx `
  -p:CanDoItAllComponentsRepositoryRoot="D:\work\CanDoItAll.Components" `
  -p:CanDoItAllFileToolsRepositoryRoot="D:\work\CanDoItAll.FileTools"
```

```sh
dotnet restore ./CanDoItAll.slnx \
  -p:CanDoItAllComponentsRepositoryRoot=/source/CanDoItAll.Components \
  -p:CanDoItAllFileToolsRepositoryRoot=/source/CanDoItAll.FileTools
```

CI checks out the Components and FileTools repositories at the commits declared in
`.github/workflows/ci.yml`, places all three repositories beside each other, and uses the
same direct project-reference graph. Docker receives those sibling repositories as named
build contexts. Reproducible local validation must record the three source commits and
keep the repository roots identical across restore, build, test, and publish.

```text
git -C ../CanDoItAll.Components rev-parse HEAD
git -C ../CanDoItAll.FileTools rev-parse HEAD
dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=true
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore -p:UseLocalCanDoItAllLibraries=true /m:1
```

Do not substitute unavailable NuGet packages for the current sibling-source contract.
The package declarations remain the portable project metadata, while
[`Directory.Build.targets`](Directory.Build.targets) removes matching declarations and
adds direct project references for the active build graph.

## Install An Instance

All supported deployment choices, common prerequisites, default data locations, and
platform-specific settings are collected in [Installing instances](docs/operations/installing-instances.md):

- [Windows](docs/operations/installing-instances.md#windows)
- [Linux](docs/operations/installing-instances.md#linux)
- [macOS](docs/operations/installing-instances.md#macos)

> **Temporary alpha-upgrade notice:** installations that retained experimental data from
> before the August 2026 portability changes may need a one-time manual data repair after
> reinstalling. If project structure pages return HTTP 500, give the
> [portable-path alpha repair prompt](docs/SpecificAlfaMigrations/2026-08-14-portable-path-data-repair.md)
> to Codex, Claude, or another coding agent. Do not add legacy fallback behavior to the
> application for this retired alpha state.

Windows has a dedicated self-contained per-user installer with a managed PostgreSQL
backend. Linux and macOS use framework-dependent artifacts and the immutable Unix release
installer, with systemd and launchd service templates respectively. The same
framework-dependent headless Web host can also be published for Windows. Container-based
development is available on any host with a Linux Compose engine.

## Development Quick Start

Run from the repository root:

```powershell
Copy-Item .env.example .env
New-Item -ItemType Directory -Force .secrets | Out-Null
Set-Content -NoNewline .secrets/db-password "replace-for-local-development"
docker compose up -d --build --wait
```

Open `http://localhost:8080`. Compose builds the Linux application image, starts its own
private PostgreSQL service, applies migrations, and preserves application and database
state in project-scoped named volumes. The ignored password file is granted only to the
app and database services.

To run the web host directly on the workstation while keeping only PostgreSQL in
Compose, copy `compose.override.yaml.example` to ignored `compose.override.yaml`, start
`db`, then run the project. See [container operations](docs/operations/containers.md).

## Build And Test

The following commands use the default sibling-source dependency mode. A normal product
build does not compile the test suites:

```powershell
dotnet restore ./CanDoItAll.slnx
dotnet build ./CanDoItAll.slnx --configuration Release --no-restore /m:1
```

During local or bundle work, build the affected production project and run only the
owning topic or exact test. Confirm discovery before treating the result as proof. This
example expects exactly one discovered test case:

```powershell
$testFilter = "FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.OpenAiRequestCompatibilityPolicyTests.Luna_chat_completions_function_tools_require_explicit_none"
$expectedDiscovery = 1

dotnet build ./src/MAF/Common/CanDoItAll.AgentFramework.Providers/CanDoItAll.AgentFramework.Providers.csproj --configuration Release /m:1
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --list-tests --filter $testFilter /m:1
# Verify that discovery reports $expectedDiscovery test case before executing it.
dotnet test ./tests/Solutions/CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter $testFilter /m:1
```

Run the documentation validator when maintained documentation or source-truth claims
change:

```powershell
./tools/Validation/Test-Documentation.ps1
```

On Windows, also validate the dedicated installer when its boundary or documentation
changes:

```powershell
./tools/install/tests/Test-CanDoItAllWebAppInstallScripts.ps1
```

The broad stable gate is reserved for CI, release or merge closure, a frozen checkpoint,
or an invalidation trigger explicitly named by the work plan. It is not a routine
per-change or per-subbundle loop. Suite entry points, filters, discovery rules, and
environment-dependent lanes are documented in [Testing](docs/testing.md). GitHub CI runs
the sibling-source stable and actual-host portability gates on Windows x64, Ubuntu x64,
and macOS arm64, plus the Linux container gate.

## Containers

The base Compose model owns a complete local **development** instance: the Linux web app,
its private PostgreSQL service, and separate named volumes for application and database
state. It is not the installed Windows web app database.

```powershell
docker compose --env-file .env.example config --quiet
docker compose --env-file .env.example up -d --build --wait
docker compose down
```

See [container operations](docs/operations/containers.md) and
[backup and restore](docs/operations/backup-and-restore.md).

## Architecture

The main dependency direction is:

![architecture](https://aicandoitall.com/assets/images/diagrams/framework-layers.svg)

Start with:

- [Architecture overview](docs/architecture/overview.md)
- [Storage, paths, and host portability](docs/architecture/storage-and-path-portability.md)
- [Runtime execution and shell portability](docs/architecture/runtime-execution-portability.md)
- [Internal communication](docs/architecture/internal-communication.md)
- [Module map](docs/architecture/modules.md)
- [Documentation index](docs/README.md)

## Styling

Application-specific Tailwind assets live under [`Tailwind`](Tailwind/README.md).

```powershell
npm install --prefix .\Tailwind
npm run tailwind:build
```

Shared component structure and styling belong to `CanDoItAll.Components`.

## Packaging

NuGet packaging and publishing are disabled for this repository. `Directory.Build.props`
sets `IsPackable` to `false` for every project. Package metadata and release tooling will
be introduced only with an explicit package contract and validation gate.

The root npm package is private and exists only to run Tailwind commands.

## License And Contributions

This repository is licensed under the [MIT License](LICENSE). The
[third-party notices](THIRD-PARTY-NOTICES.md) preserve the copyright and license terms
for external material redistributed by the application.

Code contributions are limited to partners approved by the maintainer. See
[CONTRIBUTING.md](CONTRIBUTING.md) and contact the `fyziktom` account on LinkedIn before
opening a pull request.
