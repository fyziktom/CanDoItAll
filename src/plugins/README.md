# Plugins

The plugin boundary supports optional integrations without moving product-module or
provider-neutral domain behavior into plugin implementations.

| Area | Responsibility |
|---|---|
| [Abstractions](Abstractions/README.md) | Plugin package, settings, grants, OAuth, runtime, and workflow contracts |
| [Implementations](Implementations/README.md) | Bundled Docker and email integrations |

Plugin activation is explicit and subject to capability grants, settings validation,
secret handling, and lifecycle cleanup.
