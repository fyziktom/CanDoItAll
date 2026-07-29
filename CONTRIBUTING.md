# Contributing

This repository accepts code contributions only from partners explicitly approved by the
maintainer. Unsolicited pull requests are not accepted.

To discuss becoming an approved partner, contact the `fyziktom` account on
[LinkedIn](https://www.linkedin.com/in/fyziktom/) and wait for approval before preparing
or opening a pull request.

## Development Setup

1. Install the SDK selected by `global.json`.
2. Install PostgreSQL or a Docker Compose v2 runtime.
3. Install Node.js when changing application Tailwind assets.
4. Copy `.env.example` to the ignored `.env` file when using Compose.
5. Run commands from the repository root.

## Validation

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test .\CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
& .\tools\Validation\Test-Documentation.ps1
```

Run the relevant browser, live-process, integration, or container gate when a change
affects that boundary. Report skipped, quarantined, or unavailable validation explicitly.

## Architecture Rules

- Keep Blazor rendering and orchestration in the web or owning module boundary.
- Keep product behavior in module/application services and domain rules in their owning domain.
- Keep persistence and external-system details behind infrastructure or integration adapters.
- Keep provider-neutral AgentFramework code separate from MAF, model-provider, MCP, Memory-driver, and plugin adapters.
- Use typed identifiers, commands, settings, and cross-boundary payloads.
- Treat project files, runtime composition, endpoint mapping, and migrations as authoritative.
- Keep generated output, local configuration, credentials, task proof, and runtime state out of Git.
- Update maintained documentation when public behavior, configuration, architecture, or validation changes.

## Pull Requests

- Open a pull request only after partner approval.
- Keep the change focused and identify any sibling-repository dependency.
- Add or update tests for behavior changes.
- Describe public API, data migration, security, and operational effects.
- Include exact validation commands and results.
