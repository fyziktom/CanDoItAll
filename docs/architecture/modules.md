# Module Map

Product modules live under [`src/Modules`](../../src/Modules/README.md). A module owns its
pages, navigation contribution, UI orchestration, product-facing services, and any
first-party agent tool provider for that bounded area.

| Module | Responsibility |
|---|---|
| [AgentFramework](../../src/Modules/CanDoItAll.Modules.AgentFramework/README.md) | Agent catalog, provider configuration, governed execution, conversations, and capability setup |
| [Collaboration](../../src/Modules/CanDoItAll.Modules.Collaboration/README.md) | Collaboration records and collaboration-facing application surfaces |
| [CRM/HR](../../src/Modules/CanDoItAll.Modules.CrmHr/README.md) | Parties, accounts, opportunities, workforce, recruiting, skills, staffing, and agent/person relationships |
| [Memory](../../src/Modules/CanDoItAll.Modules.Memory/README.md) | Memory provider configuration, operations, diagnostics, and user-facing Memory surfaces |
| [Plugins](../../src/Modules/CanDoItAll.Modules.Plugins/README.md) | Plugin catalog, installation, activation, grants, OAuth, settings, and logs |
| [Processes](../../src/Modules/CanDoItAll.Modules.Processes/README.md) | Process definition, launch, monitoring, recovery, assignments, and process UI |
| [Projects](../../src/Modules/CanDoItAll.Modules.Projects/README.md) | Project portfolio, hierarchy, phases, files, planning, and project-facing services |
| [Prompts](../../src/Modules/CanDoItAll.Modules.Prompts/README.md) | Prompt catalog, versions, assets, and curation surfaces |
| [Resources](../../src/Modules/CanDoItAll.Modules.Resources/README.md) | Reusable workspace resources and resource records |
| [Scheduler Planner](../../src/Modules/CanDoItAll.Modules.SchedulerPlanner/README.md) | Scheduled process/workflow launches, plans, cron projection, and run history |
| [Security](../../src/Modules/CanDoItAll.Modules.Security/README.md) | Security policy services and security-related application contracts |
| [Test Lab](../../src/Modules/CanDoItAll.Modules.TestLab/README.md) | Validation scenarios and product-facing testing workflows |
| [Workbench](../../src/Modules/CanDoItAll.Modules.Workbench/README.md) | Workbench views, canvas state, project structure, and workspace orchestration |
| [Workspace](../../src/Modules/CanDoItAll.Modules.Workspace/README.md) | Workspace settings, data sources, storage, and cross-module workspace state |

## Module Contract

Each module should:

- expose one clear dependency-injection registration entry point
- keep Razor components focused on rendering and orchestration
- place non-trivial behavior in typed services
- consume other domains through contracts rather than their UI or persistence internals
- register navigation and API/tool surfaces explicitly
- validate identifiers, ownership, authorization, and capability scope before mutation
- document its project-local build command and important entry points

Module-to-module references are acceptable only for an intentional product dependency.
Provider, transport, and persistence details remain behind their owning adapter boundary.
