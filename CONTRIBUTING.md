# Contributing

This repository accepts code contributions only from partners explicitly approved by the maintainer. Unsolicited pull requests are not accepted.

To discuss becoming an approved partner, contact [fyziktom on LinkedIn](https://www.linkedin.com/in/fyziktom/). Wait for approval before preparing or opening a pull request.

## Development Setup

1. Install the .NET SDK selected by `global.json`.
2. Start PostgreSQL as described in [Development runtime](docs/development-runtime.md).
3. Place `CanDoItAll.AgentFramework.Rag` and `CanDoItAll.AgentFramework.SemanticCompletion` beside this repository when running the full solution gate.
4. For MCP reinstall work, add sibling `CanDoItAll.Mcp` and `CanDoItAll.CodeAnalysis` repositories, plus `CanDoItAll.SharedInfo` unless skill synchronization is skipped. Add `CanDoItAll.Components` only when working on that owned boundary.
5. Install Node.js only when rebuilding Tailwind output.
6. Run commands from the repository root.

Do not commit credentials, local control-plane state, generated `.artifacts`, build output, or machine-specific paths.

## Validation

Follow [Testing](docs/testing.md). The routine Release gate is:

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test .\CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
& .\tools\Validation\Test-Documentation.ps1
```

Run the relevant extended browser, live-process, or MCP gate when the change affects that boundary. Do not report the unfiltered suite as green unless the exact unfiltered command passes.

## Architecture Rules

- Keep UI rendering and orchestration in the Blazor boundary, product behavior in modules and application services, domain rules in their owning domain, and persistence or external integration in infrastructure.
- Keep provider-neutral orchestration separate from MAF, model-provider, MCP, Memory-driver, and plugin adapters.
- Use typed contracts for identifiers, commands, settings, and cross-boundary payloads.
- Treat `.csproj` files, central package props, runtime composition, and endpoint mapping as authoritative. Do not maintain copied dependency or route inventories in prose.
- Improve shared UI packages in `CanDoItAll.Components` when a reusable component contract is missing; do not duplicate shared structure locally.
- Keep generated output and local state out of Git.
- Update maintained documentation whenever public behavior, configuration, architecture, or validation changes.

## Pull Requests

- Open a pull request only after partner approval.
- Keep changes focused and identify any sibling-repository dependency.
- Add or update tests for behavior changes.
- Describe public API, data migration, security, and operational effects.
- Include the exact validation commands and results.
- Call out quarantined or unavailable validation explicitly; do not present it as passing.
