# Copilot Instructions — CanDoItAll

Act as a pragmatic senior C#/.NET and Blazor engineer. Prefer the smallest correct change and be direct about architectural or security problems.

## Engineering Rules

- Optimize for maintainability, readability, security, and long-term evolvability.
- Use strongly typed identifiers, commands, settings, and payloads. Avoid magic strings.
- Prefer early returns, small functions, composition, and modern C# when they improve clarity.
- Do not add silent fallbacks. Fail predictably and log actionable state without secrets.
- Keep comments rare and in English. Do not add XML documentation unless requested.
- Use fully cuddled Egyptian braces and one statement per line.

## Boundaries

- `src/App/CanDoItAll.Web` renders and orchestrates the Blazor host.
- `src/App/CanDoItAll.Composition` is the application composition root.
- `src/Modules` owns product behavior and module UI.
- `src/Processes` and `src/Memory` own their provider-neutral domain/runtime contracts.
- `src/MAF` owns AgentFramework and Microsoft Agent Framework adapters, not product-module behavior.
- `src/Foundation/CanDoItAll.Infrastructure` owns persistence and external infrastructure.

Keep UI, application, domain, and infrastructure responsibilities separate. Add an interface only for a real boundary, substitution point, or test seam.

PostgreSQL is the application database. InMemory is test-only, and SQLite is retired. Generic Memory providers and workers are disabled by default. Do not restore old Cognitive Memory, Qdrant, or SQLite assumptions as hidden compatibility paths.

## Blazor And Components

- Keep components focused on rendering and orchestration; move non-trivial behavior to the owning service.
- Keep lifecycle side effects and state transitions explicit and testable.
- Reuse `CanDoItAll.Components.*` contracts before adding raw structural markup or page-local layout abstractions.
- For non-WebGL shared-component work, query the `candoitall_components` MCP: inspect libraries, request recommendations for the concrete use case, and inspect the selected component contract and examples.
- Improve the sibling `CanDoItAll.Components` library when a reusable contract is missing. Do not copy its implementation into this repository.
- WebGL libraries are outside the Components MCP workflow.
- The repository does not use Radzen. Do not introduce it incidentally.
- Tailwind is already present. Put reusable component styling in the owning shared package and application-specific styling in this repository.

## Agent And Integration Boundaries

- Treat project files, central package props, runtime composition, and endpoint mapping as source of truth.
- Keep provider-neutral agent orchestration separate from MAF, model-provider, MCP, plugin, and Memory-driver adapters.
- First-party agent tools belong in registered `IAgentRuntimeToolProvider` implementations at the owning module boundary.
- Preserve approval, capability-policy, workspace-sandbox, and process-governance checks. Never reconstruct incompatible approval state or bypass a missing tool with prompt-only behavior.
- Processes and Project Structure use the HTTP API control plane; do not reintroduce retired MCP servers.

## MCP And Skills

MCP server source lives in the sibling `CanDoItAll.Mcp` repository. This repository owns configuration and install entry points. The active sidecars are CodeAnalytics, Components, DotNetWatch, Mermaid, and SshOps.

Prefer the managed DotNetWatch MCP loop for interactive application work:

1. inspect workspace defaults
2. start or reuse the managed app
3. make focused edits
4. wait for readiness
5. validate with targeted tests and browser evidence

Use the stable commands in `docs/testing.md` for release validation. If the managed MCP is unavailable, report that explicitly before using an appropriate local validation command.

Canonical Codex development skills and plugins live in the sibling `CanDoItAll.SharedInfo` repository; this product repository does not carry a source mirror. Refresh the local toolset with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Reinstall-CanDoItAllMcps.ps1 -McpRepoRoot ..\CanDoItAll.Mcp -SharedInfoRepoRoot ..\CanDoItAll.SharedInfo
```

## Validation And Documentation

- Build after C# or project changes and run the narrowest relevant test before the stable gate.
- Use Playwright for shipped UI behavior and capture evidence at the supported large-desktop viewport.
- Never describe quarantined, skipped, unavailable, or unfiltered failing tests as green.
- Update maintained docs when public behavior, configuration, architecture, or validation changes.
- Do not treat closed `codex/bundles` or `.codex/bundles` as current documentation.
