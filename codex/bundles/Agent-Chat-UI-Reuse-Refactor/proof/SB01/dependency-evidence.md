# SB01 dependency evidence

Current project direction in the scoped product snapshot:

- `CanDoItAll.Modules.AgentFramework` -> `CanDoItAll.AgentFramework.Components`, `CanDoItAll.AppComponents`
- `CanDoItAll.Modules.Processes` -> `CanDoItAll.AgentFramework.Components`, `CanDoItAll.AppComponents`, `CanDoItAll.Modules.AgentFramework`
- No project-reference cycle exists in the scoped graph.

Pre-existing non-project findings:

- A module relationship cycle is reported between the `Modules.AgentFramework.Hosting` and `Modules.AgentFramework` namespaces within the same project.
- A type cycle is reported inside the image-generation runtime provider's nested builder types.

Neither finding crosses the proposed neutral conversation project boundary. SB02 must keep `CanDoItAll.Conversations.Components` source-neutral: BaseLib/OverlayLib and presentation contracts only, with no AgentFramework, LlmChats, backend, persistence, runtime, or service-location dependencies. The intended direction is Agent components/modules -> neutral conversation components.

Components MCP decision:

- `components_libraries_list`, correlation `corr_a148c4e24eb24c8e806e7687b586fe45`: BaseLib owns ordinary UI; OverlayLib owns bounded drag/resize/minimize/reset/hide/show surfaces.
- `components_recommend`, correlation `corr_015d584761d149b4b2626f7db82467e3`: preserve shared BaseLib composition and use controlled `OverlayWindowState` + `StateChanged` with actual container/safe-top selectors for the floating host.
- Canvas-specific floating windows are rejected because this is general product UI, not a canvas runtime.

The app-connected MCP transport had exited. The same repository-owned MCP executable was initialized over stdio using protocol `2025-06-18`; both calls completed with `ok=true` and no warnings. No component-library guidance was inferred or fabricated.

