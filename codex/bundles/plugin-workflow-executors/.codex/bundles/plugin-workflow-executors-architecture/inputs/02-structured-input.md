# Structured Input

## Target User Outcome

CanDoItAll should eventually support plugins that can be enabled/configured by users and used inside AI workflows as executor nodes.

## First Practical Target

A safe MVP for bundled/static plugins that:

1. registers plugin manifests and plugin executor descriptors;
2. displays plugins in a catalog/settings page;
3. lets users create plugin connections/configurations;
4. exposes installed/enabled plugin executors in the workflow executor catalog;
5. runs plugin executors through existing workflow timeout/retry/result semantics;
6. resolves secrets only through a runtime broker;
7. uses schema-driven settings UI or a registered bundled renderer component.

## Deferred Target

A public plugin shop that can provide catalog/package metadata to local instances. Installing remote arbitrary executable code is intentionally gated behind later trust/signature/isolation work.

## Normalized Requirements

| Id | Requirement | Acceptance Signal | Priority | Owned By |
| --- | --- | --- | --- | --- |
| R001 | Plugin system must be a separate module | Create a dedicated Plugins module and plugin abstractions instead of extending Workspace/AgentFramework pages ad hoc. | High | SB09, SB10 |
| R002 | Plugins must expose workflow executors | A plugin executor must be discoverable in workflow catalog and invokable through existing workflow runtime semantics. | High | SB02, SB12 |
| R003 | Plugin interfaces must be explicit and stable | Define plugin, manifest, executor, settings renderer, capability, connection, and package contracts with versioning. | High | SB09 |
| R004 | Plugins may use existing services safely | Expose vault, workspace files, storage, project structure, HTTP, and future OAuth through capability-gated facades, not raw IServiceProvider. | High | SB05, SB06, SB07, SB16 |
| R005 | Plugin settings must render per plugin | Each plugin can contribute a settings renderer; schema fallback must exist so simple plugins do not need custom UI. | High | SB03, SB04, SB11 |
| R006 | Plugin settings must persist separately from workflow node settings | Global/plugin connection settings and workflow-node execution settings must be separate records. | High | SB10, SB11, SB12 |
| R007 | Plugin catalog is required before settings page | Users must see bundled/installed/available plugins and installation/enabled state. | High | SB10, SB11 |
| R008 | Bundled plugins are supported first | Initial implementation uses compiled/bundled plugin providers; remote shop/package loading is designed later. | High | SB10, SB13, SB15 |
| R009 | Remote shop is future-facing | Define public-server catalog/package contract and local install state without enabling arbitrary unsigned runtime code loading in MVP. | Medium | SB15 |
| R010 | OAuth2 must be an extension point | Plugin API must anticipate OAuth2 broker and connection tokens without forcing current OAuth2 implementation. | High | SB09, SB16 |
| R011 | Secret storage must stay behind vault | Plugin settings and workflow JSON cannot store raw secrets; only references/bindings may be persisted. | Critical | SB05, SB11, SB12 |
| R012 | Secret authorization must be consumer-bound | Runtime secret resolution must enforce plugin/executor/connection ownership and allowed-secret lists. | Critical | SB05 |
| R013 | Settings schema validation must be canonical | Executor settings must be validated by a shared schema validator before runtime invocation, not only JSON syntax. | High | SB02, SB03 |
| R014 | Renderer keys must be canonical and collision-safe | Renderer keys should be namespaced by plugin id/module id and registered in a single registry. | Medium | SB04, SB09, SB11 |
| R015 | Executor catalog must include ownership/provenance | WorkflowExecutorDescriptor must expose source/plugin/version/availability/trust metadata or equivalent adjacent metadata. | High | SB02, SB12 |
| R016 | Disabled/unimplemented executors must not be silently runnable | Validation/UI/runtime must distinguish implemented, installed, enabled, disabled, unavailable, and planned executors. | High | SB02, SB12 |
| R017 | Workflow UI must not add hard-coded branches for every plugin | Move toward dynamic/schema-based editor and renderer host; built-in branch code should not be copied for plugins. | High | SB04, SB12 |
| R018 | Project structure access must use canonical gateway | Replace concrete Workbench service resolution in executors with a façade suitable for plugin capability access. | High | SB06 |
| R019 | Storage access must be scoped and policy-checked | Plugins must access workspace files/storage through scoped wrappers with path policy and declared capabilities. | High | SB06, SB07 |
| R020 | Plugin execution must be observable | Plugin executor invocations need sanitized logs/events/metrics and correlation with workflow run/node/plugin/connection. | Medium | SB07, SB17 |
| R021 | Plugin output must be sanitized and size-bounded | External-service plugin outputs must respect payload limits and avoid leaking secrets in errors/artifacts. | High | SB07, SB13, SB17 |
| R022 | Module composition must remain deterministic | MVP plugins are statically registered in composition; dynamic assembly loading waits for trust/package review. | Medium | SB10, SB15 |
| R023 | Connector schema work must be reused | Existing ConnectorConfigurationSchema/ConnectorConfigFieldEditor must be extracted/adapted rather than duplicated. | High | SB03, SB04 |
| R024 | Plugin API endpoints must be versionable | Add plugin catalog/install/connection/health APIs with DTOs independent from EF entities. | Medium | SB10, SB11, SB17 |
| R025 | Browser proof is mandatory for settings/catalog | The plugin catalog, plugin settings page, and workflow executor selection require screenshot/DOM proof. | Medium | SB11, SB12, SB17 |
| R026 | Architecture review gates are mandatory | Codex must stop for review after foundation, MVP module, and final proof before continuing. | Critical | SB08, SB14, SB18 |
| R027 | Shop packages must include compatibility metadata | Catalog/package records require min app version, plugin version, capabilities, dependencies, hash/signature fields. | Medium | SB15 |
| R028 | Plugin renderers must not be trusted by default | Remote plugin UI rendering needs trust/signature policy; bundled renderers can use DynamicComponent. | High | SB04, SB15 |
| R029 | Tests must cover duplicate and collision cases | Duplicate plugin ids/executor ids/renderer keys/settings keys must fail predictably. | Medium | SB02, SB04, SB09, SB17 |
| R030 | No raw service provider escape hatch | Plugin code must not receive arbitrary IServiceProvider access unless a specific reviewed adapter proves safe. | Critical | SB09, SB12 |
| R031 | Plugin connection health checks are required | Users need a way to validate credentials/settings before using plugin executors in workflows. | Medium | SB10, SB11, SB13 |
| R032 | Existing built-in executors remain compatible | Current workflows and descriptors must keep working while metadata/schema fields evolve. | High | SB02, SB03, SB12, SB17 |
| R033 | Remote catalog failures must be non-blocking | If shop source is unreachable, installed/bundled plugins must continue to work and UI must show clear state. | Medium | SB15, SB17 |
| R034 | OAuth tokens must not be visible to plugins as storage primitives | OAuth2 broker provides scoped token leases; plugins cannot persist refresh/access tokens themselves. | High | SB16 |
| R035 | Plugin package installation must be auditable | Install/update/enable/disable/remove actions require audit trail and manifest snapshots. | Medium | SB10, SB15, SB17 |
