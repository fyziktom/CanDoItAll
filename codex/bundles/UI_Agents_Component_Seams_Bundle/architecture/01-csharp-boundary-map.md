# Target responsibility map

| Owner | Responsibility | Must not own |
|---|---|---|
| Agents route page | Current URL codec, host composition, binding semantic workspace state to UI | EF, broad aggregation, mutable editor data, serialized URLs inside children |
| Workspace coordination | Authoritative selection, active semantic section/target, requested-open reconciliation, accessible chat context, dispatch of host intents | All editor normalization/persistence or every unrelated page feature |
| Controlled catalog | Render catalog/selection and local ephemeral filtering/expansion; emit typed intents | Workspace/provider services, dialog/chat launch, duplicate authoritative selection |
| Catalog operations | Cohesive load/repair/team mutation workflows and typed outcomes | Navigation, component references, circuit-wide UI state |
| Editor component/instance | Per-instance session/draft/edit context, typed section control, UI orchestration and presentation | Direct feature/infrastructure I/O or shared mutable service session |
| Editor operations and pure policies | Load/save/delete/capability/reference workflows; normalization and permission mapping | DialogService/NavigationManager, broad service bags, test-only session shortcuts |
| Production adapters | Existing application services, persistence and infrastructure integration | UI callbacks, route encoding, hidden global editor state |
| Existing technical children | Their owned interaction with an explicit, inventoried fakeable boundary | Unregistered surprise I/O behind a claimed isolated scenario |

~~~mermaid
flowchart TD
    Page["Agents page / host"] --> State["Workspace state and coordination"]
    Page --> Catalog["Controlled catalog"]
    Page --> Editor["Editor instance and session"]
    State --> CatalogOps["Catalog / overview operations"]
    Editor --> EditorOps["Editor operations"]
    CatalogOps --> App["Existing application services via real boundaries"]
    EditorOps --> App
    Editor --> Children["Inventoried real children"]
    Children --> Ports["Owned child capabilities / technical services"]
~~~

Arrows show collaboration, not new project references. Contract and implementation remain in the existing module during this child; later extraction moves only a proven cohesive cluster.

The page owns semantic navigation but may delegate cohesive host effects to a small coordinator/adapter with a real responsibility. A wrapper or additional port is justified by that responsibility and tests, not forbidden or required by count. Keep catalog rendering and editor state separate; do not introduce a general-purpose UI framework.
