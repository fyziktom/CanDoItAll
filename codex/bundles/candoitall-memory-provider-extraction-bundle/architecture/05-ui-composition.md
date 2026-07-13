# UI Composition

## Common generic surfaces

The generic Memory UI should provide:

- provider list and health state;
- provider profile editing and capability manifest display;
- simple query/chat with selected provider;
- operation ledger and status view;
- event inbox and routed action review;
- feedback correlation and delayed feedback review;
- source ingestion actions and history;
- empty state when no provider is configured.

## Provider-specific surfaces

Provider-specific UI can be projected by declared UI surfaces:

- `ui.rcl`: host loads compatible Blazor/RCL components by assembly/component descriptor.
- `ui.iframe`: host displays an external provider UI URL with safe framing policy and auth handshake.
- `ui.external-link`: host opens provider service UI outside the main app.
- `ui.none`: provider exposes only generic surfaces.

## Native Cognitive Memory UI

The current tabs such as dashboard, memory records, cluster search, probe workbench, curator, review queue, quality operations, recall traces, self-regulation, scale, health, settings, and sources should become native provider-specific surfaces. The generic UI should only know that the native provider exposes one or more compatible surfaces.

## Browser validation expectations

UI subbundles must prove:

- zero-provider startup does not crash;
- zero-provider query/tool actions are disabled or return typed no-provider diagnostics without selecting native memory, OpenAI, Qdrant, or a mock provider implicitly;
- mock provider profile can be configured;
- provider selection changes query target;
- failed provider shows actionable status;
- operations and feedback ledgers show stable rows;
- provider-specific RCL/iframe surfaces load or fall back safely;
- large-screen and narrower viewport layouts remain readable.
