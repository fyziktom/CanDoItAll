# Application UI

This area contains reusable UI facades owned by this application.

| Project | Responsibility |
|---|---|
| [CanDoItAll.AppComponents](CanDoItAll.AppComponents/README.md) | Application-level file, storage, dialog, and shared UI integration |
| [CanDoItAll.AppComponents.RecordBrowsing](CanDoItAll.AppComponents.RecordBrowsing/README.md) | Server-paged record browser and picker family on BaseLib only |
| [CanDoItAll.Conversations.Components](CanDoItAll.Conversations.Components/README.md) | Backend-neutral conversation presentation contracts and Blazor components |
| [CanDoItAll.Components.Git](CanDoItAll.Components.Git/README.md) | Git-focused component assembly boundary |
| [CanDoItAll.CrmHr.UI](CanDoItAll.CrmHr.UI/README.md) | CRM / HR renderers, workspace surfaces and view contracts on the CRM / HR and Projects contracts |
| [CanDoItAll.Prompts.UI](CanDoItAll.Prompts.UI/README.md) | Prompt Gallery rendering surfaces, presentation records and intents without backend dependencies |

General-purpose components belong in `CanDoItAll.Components`. Product-specific pages and
orchestration belong in the owning module.
