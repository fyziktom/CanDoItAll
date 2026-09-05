# Initial UI coupling hotspots

## Evidence and scope

The original map was prepared against development observed at 6c02b644acae3f0d05c648d6b169c82acebefea8. The v2 review checked the Agents pilot and selected source/project examples on components-decoupling at a249d77b175916d760e9f6c86633202a4ea3ae44. This remains a candidate map, not a complete whole-application audit or an execution pin.

Each child refreshes its actual callers, state/lifetime, subtree/type/reference graph, current behavior and developer-loop baseline. Do not apply one controller or project template universally.

| Area | Coupling signal | First useful boundary / future ownership |
|---|---|---|
| Agents home/catalog/details | Route/selection/context, EF/dashboard reads, dialogs/chat, mutable editor, cross-module references and child services | Current Agents child: typed state, lazy operations, controlled catalog, explicit editor lifetime; feature-owned UI |
| Small catalog/card sandbox | Catalog render/state can be isolated from editor/team host effects once actual graph is checked | Near-term extraction/host pilot after proven catalog seam, independent of production URLs |
| AgentProviderProfilesPanel | List/tree/editor/CRUD/testing/pricing/shared connections/history state | Separate provider workspace/editor slice after the small sandbox opportunity is assessed |
| ProcessWorkspaceShell | IServiceProvider hides capabilities and dependencies | Narrow explicit capability operations; useful second archetype for shared rules |
| ProjectFilesDialog | File sessions, FileTools operations, JS/preview/download/open coordination | Project-files use cases and explicit UI state; generic adapters only when genuinely shared |
| ProjectStructurePage | Many feature/runtime dependencies and a large partial cluster, including source-shape test debt | Capability-by-capability migration across surface loading, windows/selection, file/runtime/process/workflow actions |
| AppShell | Application-wide host behavior | Inventory lifetime/ownership before splitting; not a first feature pilot |
| CRM/HR, Workspace, Resources, Scheduler, Test Lab | To be refreshed per coherent user workflow | Prepare separate bounded child assessments; no blanket move into AppComponents |

## Existing useful examples

ProjectStructureCanvas already demonstrates typed input and EventCallback output with browser-specific capability needs. Some ProjectStructure policies are deterministic top-level types. Existing UI/Conversations.Components and its host separation provide a local small-UI-project example to study, not copy mechanically.

AppComponents has no direct feature-module references in the inspected project; preserve that direction. It still has its own transitive library/assets graph, so a small feature UI project should depend on it only when needed. Components/FileTools are not the primary refactoring target.

## Recommended sequence

First prove Agents seams with preserved behavior and measure the current loop. Then implement the smallest useful extraction/browser sandbox in a separately prepared child, ordinarily after Agents closure and before further broad UI refactoring. Do not wait for complete URL design or every editor descendant to become extractable.

Use a contrasting second archetype such as ProcessWorkspaceShell or ProjectFilesDialog to test which rules should become shared. Providers and the longer Workbench program remain independent bounded children, ordered by value/dependencies rather than a single mandatory linear migration.

Each larger cluster can split into multiple coherent ownership outcomes. No one-shot application-wide project split or new generic UI framework is implied.
