# Workspace completion map and exit criteria

The object of completion is the **UI/component boundary**, not removal of all Workspace backend dependencies or every Razor file. A routed host, an authorization adapter or a DI composition component may legitimately remain in the production module. It must not contain the reusable feature renderer or become a service bag. [WS25]

## Current inventory

| Surface | Current owner and state | Assignment |
|---|---|---|
| Settings route, navigation, defaults, Secrets, Files, history | Core extracted previously | Preserve all eight navigation entries and the Providers redirect; regress |
| API Access | Dedicated contracts/UI/sandbox | Preserve administration authority, sensitive lifetimes and current-denial repairs |
| Storage catalog | Dedicated contracts/UI/sandbox | Preserve SCAT-R1, staged outcomes, canonical profile and original driver semantics |
| Storage selection | Dedicated contracts/UI/sandbox with actual Agent parent | Preserve picker-only semantics, staged IDs, original callback and parent Save |
| Recovery/owner continuation | `Pages/Components/StoragePlacementRecoveryDialog.razor` | Complete W1: real read/command renderer and independent scenarios |
| Data Sources | `Pages/Components/DatabaseSourcesSettingsPanel.razor` | Complete W2: profiles, schema, transfer dialog and restart activation |
| Configuration fallback | `ConfigurationSchemaFallbackRenderer.razor` using Configuration.UI | Complete W3: move neutral rendering to the appropriate existing leaf |
| Trusted settings renderer | `SettingsRendererHost.razor` and registry | W3: retain legitimate trusted host; remove unnecessary render coupling, validate all consumers |
| MainLayout database dialog/flyout | App Web composition | Mandatory W2 regression and classification; no unrequested whole MainLayout extraction |

The directory listing and report are starting points, not an exhaustive declaration. Inventory all `.razor`, `.razor.cs`, scoped CSS, feature JS, DynamicComponent resolutions and references under Workspace and every external rendering consumer. The current registered source and UI components may have changed since review. [EV02, WS01, WS16]

## Final per-file disposition

Produce `workspace-closure-map.json` and a concise maintained document. Each current surface/file must have:

- path, component, actual callers and active route/slot;
- owner of reads, writes, authorization, lifetime and rendering;
- disposition: extracted / intentional production host / retired dead code / blocked;
- effective UI/sandbox dependency root and relevant proof IDs;
- remaining risk and a finding ID if not complete.

No `deferred` placeholder may masquerade as completion. Every true remaining renderer must be extracted or explicitly blocked with a technical reason. Do not remove a live control, feature or route to reduce the inventory. A thin wrapper has value only when it owns host lifetime, trust, effect adaptation or compatibility for a real consumer.

## Scope boundaries

W1 and W2 are independent leaf branches. W3 should reuse Configuration.UI; it is not authorization to invent a universal configuration runtime. Workbench/Project Structure, Projects, Processes, generic Agents UI and Workflow UI are tested as consumers, not subjected to another broad UI extraction here.

The test campaign must record **two separate decisions**: `workspace_ui_complete` and `application_regression_ready`. Workspace may be structurally complete while a pre-existing or refactor-related functional problem blocks release confidence. Conversely, passing general tests does not excuse a still-coupled Workspace renderer.

The execution can proceed for an extended unattended session, but completion is defined by these proofs, not by elapsed hours or by waiting until a clock time. Do not start a daemon or leave unbounded loops running.
