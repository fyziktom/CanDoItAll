# AgentFramework Common Projects

Common projects provide the core model and runtime layers used across AgentFramework
capabilities.

| Project | Responsibility |
|---|---|
| [Models](CanDoItAll.AgentFramework.Models/README.md) | Typed agent, execution, provider, workflow, and policy models |
| [Core](CanDoItAll.AgentFramework.Core/README.md) | Provider-neutral execution and workspace orchestration |
| [Providers](CanDoItAll.AgentFramework.Providers/README.md) | Provider catalog and selection |
| [Provider pipelines](CanDoItAll.AgentFramework.ProviderPipelines/README.md) | Ordered provider transformation pipelines |
| [Persistence](CanDoItAll.AgentFramework.Persistence/README.md) | AgentFramework persistence and template seeding |
| [Hosting](CanDoItAll.AgentFramework.Hosting/README.md) | Host-level AgentFramework registration |
| [MAF adapter](CanDoItAll.AgentFramework.Maf/README.md) | Microsoft Agent Framework and model-provider integration |
| [Voice](CanDoItAll.AgentFramework.Voice/README.md) | Voice-provider contracts and orchestration |
| [Components](CanDoItAll.AgentFramework.Components/README.md) | AgentFramework-focused Blazor components |

Core orchestration stays provider-neutral. Adapter, persistence, hosting, and UI concerns
remain in their named projects.
