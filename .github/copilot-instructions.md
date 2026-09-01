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

PostgreSQL is the application database. InMemory is test-only. Generic Memory providers and workers are disabled by default. Cognitive Memory, Qdrant, and SQLite are not base runtime dependencies.

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
- Processes and Project Structure use the HTTP API control plane.

## External Development Tooling

MCP server source and setup belong to `CanDoItAll.Mcp`. Canonical Codex skills, plugins,
and repository-family standards belong to `CanDoItAll.SharedInfo`. Keep developer-machine
launch configuration and local MCP settings outside this repository.

## Validation And Documentation

- Build each affected production project after C# or project changes, then run the
  narrowest relevant test with a stated and confirmed discovery count. Run the broad
  stable gate only for CI, release/merge closure, a frozen checkpoint, or a named
  invalidation trigger from `docs/testing.md`.
- Treat `portability-static` as a mandatory closure gate for every change under
  `.github`, `src`, `Templates`, or `tools`, and for protected root build/configuration
  files. Follow the review-and-refresh procedure in `docs/testing.md`: repair genuine
  portability defects; refresh the reviewed baseline in the same change only for
  intentional findings; inspect its diff; and rerun enforcement without
  `--write-baseline`. Never dismiss `ADDED` or `STALE` findings as CI-only or close the
  task while this gate is failing.
- Use Playwright for shipped UI behavior and capture evidence at the supported large-desktop viewport.
- Never describe quarantined, skipped, unavailable, or unfiltered failing tests as green.
- Update maintained docs when public behavior, configuration, architecture, or validation changes.
- Keep architecture and operator guidance under `docs` and project boundaries in local READMEs.
