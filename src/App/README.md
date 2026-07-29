# Application Hosts

This area contains the executable application boundary and its composition root.

| Project | Responsibility |
|---|---|
| [CanDoItAll.Web](CanDoItAll.Web/README.md) | Blazor host, HTTP API, OpenAPI, and transport mapping |
| [CanDoItAll.Composition](CanDoItAll.Composition/README.md) | Dependency injection, modules, infrastructure, and runtime startup |

The web project must remain thin: application-wide registration belongs in Composition,
while product behavior belongs in the owning module or domain.
