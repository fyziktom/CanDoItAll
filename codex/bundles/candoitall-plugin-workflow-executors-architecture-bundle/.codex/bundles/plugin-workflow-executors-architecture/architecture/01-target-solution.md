# Target Plugin Solution

## Layering

```text
CanDoItAll.Plugins.Abstractions
  - Plugin ids, descriptors, manifests, capabilities
  - Plugin settings schema contracts or references to shared settings schema
  - Plugin workflow executor contracts
  - Plugin connection/auth contracts
  - Plugin service capability interfaces
  - No dependency on implementation modules

CanDoItAll.Modules.Plugins
  - Catalog, installation state, connection state
  - Bundled plugin source
  - API endpoints and settings/catalog pages
  - Renderer registry host
  - Plugin executor bridge into AgentFramework workflow executors
  - Integration with secret broker, workspace files, project gateway, OAuth2 broker

CanDoItAll.AgentFramework.Core/Models
  - Existing workflow executor runtime contracts
  - Descriptor hardening and validator support
  - Invoker and policy semantics remain canonical

CanDoItAll.Modules.Security
  - Vault remains internal
  - Plugin secret broker resolves secret values at runtime only

CanDoItAll.Modules.Workspace / SharedKernel
  - Canonical settings schema extracted/adapted from connector schema
  - Workspace file/scoped access facilities

CanDoItAll.Modules.Workbench/Projects
  - Project structure gateway implementation
```

## Runtime Flow

```text
User opens plugin catalog
  -> Plugins module lists bundled/installed/shop-available manifests
  -> User enables bundled plugin
  -> User creates plugin connection/settings
  -> Settings renderer validates schema and secret bindings
  -> Plugin health check runs through capability context

User edits workflow
  -> Workflow executor catalog includes enabled plugin executors
  -> Workflow node stores executor id + connection reference + node settings
  -> Validator checks availability, settings schema, connection binding, policy
  -> Workflow executor invoker calls plugin bridge
  -> Plugin bridge builds PluginExecutionContext and capability context
  -> Plugin executor runs under timeout/retry policy
  -> Result is normalized, size-bounded, and sanitized
```

## MVP Trust Model

- Bundled/static plugins are trusted application code.
- Remote shop entries are metadata only until package trust review.
- Plugin settings renderer components are trusted only when compiled into bundled application assemblies.
- Schema fallback renderer handles remote/untrusted catalog metadata.

## Canonical Design Principles

1. Workflows remain the canonical executor runtime.
2. Settings schema is shared, not duplicated.
3. Secrets resolve only at runtime through a broker.
4. Service access is capability-gated.
5. Plugin installation state is separate from plugin connection state.
6. Workflow node settings are separate from plugin/global settings.
7. Public shop support is designed as metadata first; executable code loading is reviewed later.
