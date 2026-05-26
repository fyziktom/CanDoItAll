# Target Solution

## Template shape

The reusable Blazor app delivery process is a generic Blazor WASM PWA delivery template. It may mention Blazor, .NET, PWA, browser proof, service workers, manifests, screenshots, console proof, and project-structure writeback. It must not mention a specific app topic, game mechanic, domain sample, route name, class name, or acceptance criterion that belongs to a single demonstration.

## Seed and live-run shape

The seed catalog separates regression scenarios from live-run profiles:

- Seeded regression scenarios may include completed transitions and artifacts for contract and recovery tests.
- Live-run profiles must contain assignments, generic run-start guidance, and acceptance-input placeholders only.
- Live-run profiles must not contain completed transitions or artifacts.

## Runtime shape

Runtime code remains generic. It reasons about typed operations, target scopes, required tools, artifacts, block causes, recovery options, work briefs, and current-run evidence. It must not branch on an app topic.

## UI/API shape

The API exposes enough profile metadata to start or inspect a generic Blazor WASM PWA live run. The UI or runbook can use that profile with a user-supplied project-structure node and acceptance criteria for any app topic.
