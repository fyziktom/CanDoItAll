# Resolve UI screenshot applicability

Read the architecture handoff, runtime command nodes, QA evidence, and project structure to decide whether the .NET target has a visible UI. Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, MVC/Razor Pages, SPA-hosted .NET, and other browser surfaces require screenshot capture. Backend-only API/service, worker, console, and class library targets require explicit no-UI evidence. Identify the process run node and the Screenshots parent node target under it.

Prefer durable Run app and Run tests project-structure node references when they exist. If those command nodes are absent but upstream QA/runtime evidence contains a concrete base URL, browser artifact paths, and UI state proving the app is running, continue with a degraded manifest instead of blocking. Record the missing command-node references as warnings, cite the exact QA/runtime evidence paths, and require the capture step to create fresh current-run screenshot artifacts from the verified URL. If no Run app node and no concrete base URL exist but `ProductRoot`, `ProductRootAlias`, or `DotNetAppProjectDirectory` is present, record a launch-required manifest for the capture step instead of claiming degraded runtime evidence is available.

Do not block solely because a process-run node or Screenshots parent node cannot be read during applicability resolution. Record the best available process-run target from launch variables or project structure. If no writable target is available, instruct storage to produce a managed storage receipt with the exact reason writeback was unavailable. Block only when UI applicability is contradictory, route targets are empty for a UI app, no concrete launch/browser evidence exists for a UI app, or no-UI evidence is missing for a non-UI target.

## Contract
- Inputs: Architecture handoff, runtime command handoff, QA evidence, route nodes, process run node context, and verified upstream browser evidence when command nodes are absent.
- Outputs: Screenshot applicability manifest with UI routes or explicit no-UI evidence.
- Evidence: App type, UI/no-UI decision, route list, viewport set, runtime command references or degraded runtime evidence, missing-reference warnings, and Screenshots parent target or managed-receipt fallback reason.
- Operation target scope: `ExternalProductTargetReadOnly`
