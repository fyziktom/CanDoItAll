# Resolve UI screenshot applicability

Read the architecture handoff, runtime command nodes, QA evidence, and project structure to decide whether the .NET target has a visible UI. Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, MVC/Razor Pages, SPA-hosted .NET, and other browser surfaces require screenshot capture. Backend-only API/service, worker, console, and class library targets require explicit no-UI evidence. Identify the process run node and the Screenshots parent node target under it.

## Contract
- Inputs: Architecture handoff, runtime command handoff, QA evidence, route nodes, and process run node context.
- Outputs: Screenshot applicability manifest with UI routes or explicit no-UI evidence.
- Evidence: App type, UI/no-UI decision, route list, viewport set, runtime command references, and Screenshots parent target.
- Operation target scope: `ExternalProductTargetReadOnly`
