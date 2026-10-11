# Application UI

This area contains reusable UI facades owned by this application.

The [current components closure](../../docs/architecture/components-decoupling-closure.md)
and [source register](../../docs/architecture/components-decoupling-register.json) are the
authoritative cross-product map. They also cover renderers outside this directory, native
route/effect hosts, contributed content, compatibility identities and assets.

| Project | Responsibility |
|---|---|
| [CanDoItAll.Application.UI](CanDoItAll.Application.UI/README.md) | Dashboard, runtime capability facts, safe profile dialog, shell utilities and status presentation |
| [CanDoItAll.AppComponents](CanDoItAll.AppComponents/README.md) | Application-level file, storage, dialog, and shared UI integration |
| [CanDoItAll.AppComponents.RecordBrowsing](CanDoItAll.AppComponents.RecordBrowsing/README.md) | Server-paged record browser and picker family on BaseLib only |
| [CanDoItAll.Conversations.Components](CanDoItAll.Conversations.Components/README.md) | Backend-neutral conversation presentation contracts and Blazor components |
| [CanDoItAll.Components.Git](CanDoItAll.Components.Git/README.md) | Retained assembly marker; no current renderer |
| [CanDoItAll.CrmHr.UI](CanDoItAll.CrmHr.UI/README.md) | CRM / HR renderers, workspace surfaces and view contracts on the CRM / HR and Projects contracts |
| [CanDoItAll.Prompts.UI](CanDoItAll.Prompts.UI/README.md) | Prompt Gallery rendering surfaces, presentation records and intents without backend dependencies |
| [CanDoItAll.Collaboration.UI](CanDoItAll.Collaboration.UI/README.md) | Complete Collaboration workspace over a host-owned view contract, with implementation-free models and real BaseLib children |
| [CanDoItAll.Plugins.UI](CanDoItAll.Plugins.UI/README.md) | Complete Plugins workspace, connection drafts and package upload surface over typed host actions |

General-purpose components belong in `CanDoItAll.Components`. Product-specific pages and
orchestration belong in the owning module.

| Family | Rendering owners |
|---|---|
| Workbench | [Structure](CanDoItAll.Workbench.Structure.UI/README.md), [Planning](CanDoItAll.Workbench.Planning.UI/README.md), [Insights](CanDoItAll.Workbench.Insights.UI/README.md), [Content](CanDoItAll.Workbench.Content.UI/README.md), [Execution](CanDoItAll.Workbench.Execution.UI/README.md), [Operators](CanDoItAll.Workbench.Operators.UI/README.md) |
| Workspace | [General settings](CanDoItAll.Workspace.UI/README.md), [API access](CanDoItAll.Workspace.ApiAccess.UI/README.md), [Data Sources](CanDoItAll.Workspace.DataSources.UI/README.md), [Storage catalog](CanDoItAll.Workspace.StorageCatalog.UI/README.md), [Recovery](CanDoItAll.Workspace.StorageRecovery.UI/README.md), [Selection](CanDoItAll.Workspace.StorageSelection.UI/README.md), [Configuration fields](CanDoItAll.Configuration.UI/README.md) |
| Agents | [Catalog and usage](CanDoItAll.AgentFramework.UI/README.md), [Editor](CanDoItAll.AgentFramework.Editor.UI/README.md), [Capabilities](CanDoItAll.AgentFramework.CapabilityAuthoring.UI/README.md), [Providers](CanDoItAll.AgentFramework.Providers.UI/README.md), [Shared providers](CanDoItAll.AgentFramework.SharedProviders.UI/README.md), [Workflow authoring](CanDoItAll.AgentFramework.WorkflowAuthoring.UI/README.md) |
| Projects and execution | [Projects](CanDoItAll.Projects.UI/README.md), [Files](CanDoItAll.Projects.Files.UI/README.md), [Processes](CanDoItAll.Processes.UI/README.md), [Scheduler](CanDoItAll.SchedulerPlanner.UI/README.md) |
| Other workspaces | [Resources](CanDoItAll.Resources.UI/README.md), [Memory](CanDoItAll.Memory.UI/README.md), [TestLab](CanDoItAll.TestLab.UI/README.md) |
| Shared composition | [Embedded browser](CanDoItAll.AppComponents.EmbeddedBrowser/README.md), [Conversation shell](CanDoItAll.Conversations.Shell/README.md) |

The conversation shell is a generic contributor/lifecycle host. Its product conversation
controllers remain native and compose the neutral conversation components. It is not a
standalone replacement for the Agent or Simple Chat owners.

[TestLab.UI](CanDoItAll.TestLab.UI/README.md) renders the complete TestLab workspace with
host-owned state and pure draft/submission reconciliation, without backend dependencies.
