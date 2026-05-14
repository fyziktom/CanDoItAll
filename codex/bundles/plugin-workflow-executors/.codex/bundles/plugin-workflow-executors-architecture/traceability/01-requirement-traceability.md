# Requirement Traceability

| Requirement | Title | Priority | Subbundles | Acceptance Signal |
| --- | --- | --- | --- | --- |
| R001 | Plugin system must be a separate module | High | SB09, SB10 | Create a dedicated Plugins module and plugin abstractions instead of extending Workspace/AgentFramework pages ad hoc. |
| R002 | Plugins must expose workflow executors | High | SB02, SB12 | A plugin executor must be discoverable in workflow catalog and invokable through existing workflow runtime semantics. |
| R003 | Plugin interfaces must be explicit and stable | High | SB09 | Define plugin, manifest, executor, settings renderer, capability, connection, and package contracts with versioning. |
| R004 | Plugins may use existing services safely | High | SB05, SB06, SB07, SB16 | Expose vault, workspace files, storage, project structure, HTTP, and future OAuth through capability-gated facades, not raw IServiceProvider. |
| R005 | Plugin settings must render per plugin | High | SB03, SB04, SB11 | Each plugin can contribute a settings renderer; schema fallback must exist so simple plugins do not need custom UI. |
| R006 | Plugin settings must persist separately from workflow node settings | High | SB10, SB11, SB12 | Global/plugin connection settings and workflow-node execution settings must be separate records. |
| R007 | Plugin catalog is required before settings page | High | SB10, SB11 | Users must see bundled/installed/available plugins and installation/enabled state. |
| R008 | Bundled plugins are supported first | High | SB10, SB13, SB15 | Initial implementation uses compiled/bundled plugin providers; remote shop/package loading is designed later. |
| R009 | Remote shop is future-facing | Medium | SB15 | Define public-server catalog/package contract and local install state without enabling arbitrary unsigned runtime code loading in MVP. |
| R010 | OAuth2 must be an extension point | High | SB09, SB16 | Plugin API must anticipate OAuth2 broker and connection tokens without forcing current OAuth2 implementation. |
| R011 | Secret storage must stay behind vault | Critical | SB05, SB11, SB12 | Plugin settings and workflow JSON cannot store raw secrets; only references/bindings may be persisted. |
| R012 | Secret authorization must be consumer-bound | Critical | SB05 | Runtime secret resolution must enforce plugin/executor/connection ownership and allowed-secret lists. |
| R013 | Settings schema validation must be canonical | High | SB02, SB03 | Executor settings must be validated by a shared schema validator before runtime invocation, not only JSON syntax. |
| R014 | Renderer keys must be canonical and collision-safe | Medium | SB04, SB09, SB11 | Renderer keys should be namespaced by plugin id/module id and registered in a single registry. |
| R015 | Executor catalog must include ownership/provenance | High | SB02, SB12 | WorkflowExecutorDescriptor must expose source/plugin/version/availability/trust metadata or equivalent adjacent metadata. |
| R016 | Disabled/unimplemented executors must not be silently runnable | High | SB02, SB12 | Validation/UI/runtime must distinguish implemented, installed, enabled, disabled, unavailable, and planned executors. |
| R017 | Workflow UI must not add hard-coded branches for every plugin | High | SB04, SB12 | Move toward dynamic/schema-based editor and renderer host; built-in branch code should not be copied for plugins. |
| R018 | Project structure access must use canonical gateway | High | SB06 | Replace concrete Workbench service resolution in executors with a façade suitable for plugin capability access. |
| R019 | Storage access must be scoped and policy-checked | High | SB06, SB07 | Plugins must access workspace files/storage through scoped wrappers with path policy and declared capabilities. |
| R020 | Plugin execution must be observable | Medium | SB07, SB17 | Plugin executor invocations need sanitized logs/events/metrics and correlation with workflow run/node/plugin/connection. |
| R021 | Plugin output must be sanitized and size-bounded | High | SB07, SB13, SB17 | External-service plugin outputs must respect payload limits and avoid leaking secrets in errors/artifacts. |
| R022 | Module composition must remain deterministic | Medium | SB10, SB15 | MVP plugins are statically registered in composition; dynamic assembly loading waits for trust/package review. |
| R023 | Connector schema work must be reused | High | SB03, SB04 | Existing ConnectorConfigurationSchema/ConnectorConfigFieldEditor must be extracted/adapted rather than duplicated. |
| R024 | Plugin API endpoints must be versionable | Medium | SB10, SB11, SB17 | Add plugin catalog/install/connection/health APIs with DTOs independent from EF entities. |
| R025 | Browser proof is mandatory for settings/catalog | Medium | SB11, SB12, SB17 | The plugin catalog, plugin settings page, and workflow executor selection require screenshot/DOM proof. |
| R026 | Architecture review gates are mandatory | Critical | SB08, SB14, SB18 | Codex must stop for review after foundation, MVP module, and final proof before continuing. |
| R027 | Shop packages must include compatibility metadata | Medium | SB15 | Catalog/package records require min app version, plugin version, capabilities, dependencies, hash/signature fields. |
| R028 | Plugin renderers must not be trusted by default | High | SB04, SB15 | Remote plugin UI rendering needs trust/signature policy; bundled renderers can use DynamicComponent. |
| R029 | Tests must cover duplicate and collision cases | Medium | SB02, SB04, SB09, SB17 | Duplicate plugin ids/executor ids/renderer keys/settings keys must fail predictably. |
| R030 | No raw service provider escape hatch | Critical | SB09, SB12 | Plugin code must not receive arbitrary IServiceProvider access unless a specific reviewed adapter proves safe. |
| R031 | Plugin connection health checks are required | Medium | SB10, SB11, SB13 | Users need a way to validate credentials/settings before using plugin executors in workflows. |
| R032 | Existing built-in executors remain compatible | High | SB02, SB03, SB12, SB17 | Current workflows and descriptors must keep working while metadata/schema fields evolve. |
| R033 | Remote catalog failures must be non-blocking | Medium | SB15, SB17 | If shop source is unreachable, installed/bundled plugins must continue to work and UI must show clear state. |
| R034 | OAuth tokens must not be visible to plugins as storage primitives | High | SB16 | OAuth2 broker provides scoped token leases; plugins cannot persist refresh/access tokens themselves. |
| R035 | Plugin package installation must be auditable | Medium | SB10, SB15, SB17 | Install/update/enable/disable/remove actions require audit trail and manifest snapshots. |
