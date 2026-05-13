# Gaps And Prerequisites Before Plugin Module Work

## Prerequisite 1: Executor Descriptor And Availability Model

Add plugin-ready metadata before exposing plugin executors:

- source kind: built-in, bundled plugin, local package, remote shop package;
- plugin id and plugin version when applicable;
- executor availability: implemented, planned, installed, enabled, disabled, unavailable, incompatible;
- trust level and capability summary;
- settings schema id/version;
- optional connection requirements.

This prevents planned/broken/uninstalled plugin executors from behaving like runnable built-ins.

## Prerequisite 2: Canonical Settings Schema

The current connector schema should be extracted/adapted into a shared configuration schema. Do not create a separate plugin settings schema unless the connector schema is intentionally replaced everywhere.

Required shape:

- field key, label, type, required flag, help text;
- enum/select fields;
- secret reference fields with purpose restrictions;
- validation rules and defaults;
- schema version;
- JSON serialization state;
- redacted summary for UI/logs.

## Prerequisite 3: Settings Renderer Host

Build one renderer host with:

- schema fallback renderer;
- bundled Razor component registry;
- collision-safe renderer keys;
- clear renderer trust model;
- component tests and browser proof.

Workflow canvas should use this host instead of hard-coded per-executor editor branches.

## Prerequisite 4: Secret Runtime Authorization

Before plugins can request secrets:

- every secret resolution must include consumer type and consumer id;
- plugin connection secret bindings must be persisted as ids/references only;
- secret purposes must include plugin connection/executor purposes;
- resolver must reject secrets not explicitly bound to that plugin/connection/executor;
- logs and validation messages must be redacted.

## Prerequisite 5: Capability-Gated Plugin Services

Expose capabilities through narrow facades:

- `IPluginWorkspaceFiles`;
- `IPluginStorageGateway` only for declared storage-provider capabilities;
- `IPluginProjectStructureGateway`;
- `IPluginSecretBroker`;
- `IPluginHttpClientFactory`;
- `IPluginOAuth2Broker` as future extension point;
- `IPluginExecutionEvents` or equivalent audit sink.

Do not expose raw `IServiceProvider`, raw vault, raw storage registry, or concrete Workbench services.

## Prerequisite 6: Composition Discipline

MVP plugin module should be added statically through current composition. Do not introduce dynamic assembly loading until package signature, trust, isolation, version compatibility, and unload strategy have passed review.
