# Agent Capabilities

Capability projects define the typed catalog and access-policy boundary used before tools,
skills, providers, or MCP surfaces are attached to an agent execution.

| Project | Responsibility |
|---|---|
| [Abstractions](CanDoItAll.AgentFramework.Capabilities.Abstractions/README.md) | Capability identifiers, enums, models, and naming rules |
| [Access](CanDoItAll.AgentFramework.Capabilities.Access/README.md) | Effective access-policy evaluation |
| [Templates](CanDoItAll.AgentFramework.Capabilities.Templates/README.md) | Template DTO validation and policy compilation |

Capability policy can restrict an available capability; it cannot create authority that
was not assigned by the owning application boundary.
