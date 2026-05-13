# Implementation Weak Point Inventory

| Area | Evidence | Weak point | Refactor direction |
| --- | --- | --- | --- |
| Manifest capabilities | `PluginCapabilityKind` has no host command, Docker, PowerShell, or grant state. | Static declaration cannot enforce runtime consent. | Add grant model and optional host-tool capability without making Docker core. |
| Capability context | `IPluginCapabilityContext` exposes capability properties. | No visible denied proxy or centralized evaluator. | Build context through grant-aware proxies per invocation. |
| Catalog persistence | `PluginInstallationRecord` stores install snapshot only. | No permissions, connections, or runtime policy state. | Add separate grant and connection records with indexes and concurrency. |
| Catalog service | `PluginInstallRequest` and update request carry actor strings. | Audit actor can be forged if request body is trusted. | Derive actor from auth/system context. |
| Plugin UI | `PluginsPage.razor` is read-only catalog display. | User cannot grant, revoke, configure, or health-check plugin access. | Add settings, connections, and permission management UI. |
| Plugin API | `PluginsApi` only lists/install/enables/disables. | No grant or connection command endpoints. | Add focused API/application service operations. |
| Secret broker | Abstraction and security modules define duplicate plugin secret references and purposes. | Contract drift and adapter confusion. | Unify or add explicit adapter owned by security boundary. |
| Workflow executor bridge | Existing invoker handles built-in executor execution and payload caps. | Plugin bridge and grant-aware availability are incomplete. | Register plugin executors through catalog and enforce grants in validation/runtime. |
| Host execution | `WorkspaceCommandExecutionService` has reviewed recipes but is too broad for plugins. | Raw exposure would allow non-plugin recipes and unsafe env inheritance. | Create narrower plugin host-tool facade. |
| Environment policy | `WorkspaceCommandEnvironmentPolicy` allows `OPENAI_API_KEY` and `OPENAI_`. | Plugin process could inherit unrelated credentials. | Add plugin-safe environment policy and explicit secret injection only through grants. |
| Process host | `LocalWorkspaceProcessHost` is policy-only local boundary. | Not a sandbox; must not be advertised as isolation. | Surface boundary in audit and require grants/risk labels. |
| Output payloads | Plugin payload cap is applied after executor returns. | Docker logs can consume memory and produce oversized artifacts before cap. | Apply caps at recipe capture, plugin result shaping, workflow input, and storage. |
| EF queries | Current catalog loads all descriptors/installations. | OK now, weak for future shop, settings pages, and per-run grant checks. | Use projections, paging, indexes, and run-scoped grant snapshots. |
