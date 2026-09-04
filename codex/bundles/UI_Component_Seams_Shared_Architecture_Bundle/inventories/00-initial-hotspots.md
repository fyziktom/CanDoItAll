# Initial UI coupling hotspots

## Baseline note

This inventory was prepared against `development` observed at
`6c02b644acae3f0d05c648d6b169c82acebefea8`. It is a starting map, not an execution pin
or complete audit. Every child bundle must refresh the selected source area.

## Candidate map

| Area | Current coupling signal | Likely first seam | Future destination | Relative priority |
|---|---|---|---|---|
| `AgentsHomePage` | Workspace, usage, chat, catalog warmup, notifications/dialogs, and direct `IDbContextFactory<AppDbContext>` in the route page | Typed workspace state plus one overview query boundary; remove direct EF from Razor | AgentFramework module UI | High; recommended first vertical slice |
| `AgentCatalogPanel` | Loads agents/teams/providers, repairs catalog, owns selection, opens details/team dialogs, and launches quick chat | Controlled catalog state and typed intents; coherent catalog controller where needed | AgentFramework module UI | High |
| `AgentDetailsDialog` | Coordinates AgentFramework, provider, Projects, Secrets, storage registry, CRUD, dialogs, notifications, and local numeric section index | Stable editor section state plus cohesive editor use-case/controller boundary | AgentFramework module UI | High |
| `AgentProviderProfilesPanel` | Only a few injected services, but owns list/tree, editor, CRUD, tests, pricing refresh, shared connections, history-related state, and selection | Provider workspace state and provider editor workflow boundary | AgentFramework module UI | High after Agents catalog/editor seam |
| `ProcessWorkspaceShell` | Injects `IServiceProvider` in a Razor component and hides optional dependencies | Explicit capability/operation contracts; remove service locator | Processes module UI | High, small surgical boundary |
| `ProjectFilesDialog` | Coordinates project file session, FileTools actions, interaction composition, JS, logging, preview/download/open behavior | Project-files workflow controller plus explicit render state | Projects module UI; generic FileTools adapters stay AppComponents | Medium/high |
| `ProjectStructurePage` | Roughly twenty direct dependencies, many feature/runtime capabilities, large markup, and 22 partial class files frozen by a source-shape test | Strangler extraction by capability: surface loading, selection, windows, file/runtime/process/workflow actions | Workbench module UI | Critical long-term, unsuitable as first pilot |
| `AppShell` | Large application-wide component with significant shell behavior | Inventory state ownership before any split; retain application-wide semantics | AppComponents | Observe; not first feature pilot |

## Positive existing seams

Not every component is equally coupled.

- `ProjectStructureCanvas` already follows a useful pattern: typed surface input,
  `EventCallback` outputs, and browser-specific `IJSRuntime` usage.
- Several ProjectStructure policies have already been moved to top-level deterministic
  types rather than new partial files.
- `AppComponents` currently has a relatively narrow project graph and no direct feature
  module reference. Preserve this property.
- General Components and FileTools libraries are not the primary target of this program.

## Recommended candidate sequence

This is a planning order only, not an executable subbundle list:

```text
1. Agents route page + catalog + details editor seam
2. Provider workspace/editor seam
3. ProcessWorkspaceShell service-locator removal
4. ProjectFilesDialog workflow seam
5. ProjectStructurePage capability-by-capability strangler program
6. Additional CRM/HR, Workspace, Resources, Scheduler, and Test Lab clusters
```

Prepare each as a separate child bundle. Split again when a candidate contains more than
one coherent ownership outcome.
