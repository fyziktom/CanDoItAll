# Assumptions And Risks

## Working Assumptions

- ManagedCode.MarkItDown remains the canonical document conversion implementation.
- Surface-specific access checks, receipts, and approval policy stay in runtime-tool and executor adapters; shared application operations own only common behavior.
- Plugin settings renderers are trusted application registrations. Untrusted/external plugins use schema rendering and cannot activate arbitrary component type names.
- Workflow lifecycle and usage persistence may require a migration; schema changes are allowed when backed by integration tests.
- Large-screen means the existing maximized desktop workflow route. No small- or medium-screen design pass is required.

## Critical Path Risks

- Moving active interfaces can cause broad namespace churn. Move one contract family at a time and keep the build green.
- A generic arbitrary-shell executor expands attack surface. The command executor must expose typed, policy-governed recipes or remain non-runnable until safe.
- Replacing source-ingestion extraction can alter formatting and supported extensions. Capture character limits, diagnostics, and error behavior before changing it.
- Lifecycle persistence changes affect cancellation, human input, and backend semantics. Do not claim durability for the in-process backend.
- Usage may be duplicated across provider, executor, native, and normalized events. Persist observation identity and define one aggregation source.
- Plugin descriptor consolidation can reveal invalid manifests. Fail validation explicitly with plugin and executor IDs.

## Validation Risks

- File-size/partial-class tests can pass while boundaries remain wrong; replace them with behavior and dependency tests.
- Catalog-only UI tests miss create-form and renderer-host defects; test catalog, component rendering, and browser interaction.
- Cost totals can look plausible while unknown observations become zero; test known and unknown usage separately.
- A DI-registered bridge can appear complete without a caller; process and agent paths require integration proof.
- Components MCP must be retried before UI work; if still unavailable, record the gap and prove reuse through repository components plus browser evidence.

## Reopen Triggers

- Any new Core-to-Runtime reference, dependency cycle, or service-locator resolution.
- Any plugin renderer activated from a manifest type-name string without an allow-listed registration.
- Any executor/tool reimplementing document, file, spreadsheet, or image behavior already owned by a shared service.
- Any analytics total that cannot explain provider, model, observation count, unknown usage, and duration source.
- Any shipped executor missing from catalog, invoker, create UI, schema editor, preview/simulation contract, or tests.
