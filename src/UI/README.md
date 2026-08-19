# Application UI

This area contains reusable UI facades owned by this application.

| Project | Responsibility |
|---|---|
| [CanDoItAll.AppComponents](CanDoItAll.AppComponents/README.md) | Application-level file, storage, dialog, and shared UI integration |
| [CanDoItAll.Conversations.Components](CanDoItAll.Conversations.Components/README.md) | Backend-neutral conversation presentation contracts and Blazor components |
| [CanDoItAll.Components.Git](CanDoItAll.Components.Git/README.md) | Git-focused component assembly boundary |

General-purpose components belong in `CanDoItAll.Components`. Product-specific pages and
orchestration belong in the owning module.
