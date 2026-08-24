# Current Workspace persistence and UI

## Canonical persistence

`src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs` defines the EF Core
`CanDoItAll.Modules.Workspace.ProviderProfile` entity mapped to
`Workspace_ProviderProfiles`.

Its current persisted fields include:

- local `Id` and `Name`;
- optional legacy provider kind;
- connector plugin key and configuration schema version;
- base URL;
- API-key secret identifier;
- default model and timeout;
- enabled and capability flags;
- health state;
- extra settings JSON;
- application-managed concurrency token.

`WorkspaceService.SaveProviderAsync` validates connector manifests, schema, required secret
reference, timeout, capabilities, and pricing before writing the row. It then notifies
`IWorkspaceProviderProfileCommitObserver` implementations.

This makes Workspace EF data the provider master. The AgentFramework catalog is a projection,
not a second authority.

## Runtime projection

`WorkspaceBackedAgentProviderProfileRegistry` reads the Workspace row and projects it to the
AgentFramework catalog. `WorkspaceAgentProviderProfileMapper` currently maps connector keys to
provider kind, transport, purpose, tags, models, and secret references using explicit logic.

The shared connector belongs here as an origin mapping to an OpenAI-compatible effective
profile. Inner MAF projects must not learn about Workspace source/import entities.

## Connector manifests and dynamic UI

`ProviderExecution.cs` defines `IProviderAdapter`, `ProviderRegistry`, connector field keys,
and manifests. OpenAI, Ollama, remote Ollama, ComfyUI, scenario, and process fixtures follow
this pattern.

The provider management panel is already manifest/schema driven. This is a useful extension
point: a shared connector can have typed metadata and health behavior without adding another
large hard-coded provider form.

## UI ownership challenge

Provider UI lives in the Workspace module, and the AgentFramework module already references
Workspace. Creating a new feature module that references Workspace and then making Workspace
reference it would cause a cycle.

The preferred implementation keeps publication/source/import application ownership in
Workspace but uses cohesive new top-level files and services. It must not append another large
partial section to `WorkspaceModels.cs`.

## Data model gap

Current provider rows cannot truthfully represent:

- a separate public publication identity;
- one shared source with one credential reference;
- a stable import relationship;
- remote source identity and ETag;
- remote availability/revision/capability snapshot;
- local alias versus remote display name;
- invocation audit and usage attribution.

These require explicit relational entities and indexes. `ExtraSettingsJson` may carry
versioned provider metadata but may not be the only source/import/publication model.
