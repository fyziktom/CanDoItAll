# Dependency direction and placement

Target direction for future extraction: host -> feature UI + production adapters; UI -> lightweight contracts and selected UI libraries; adapters -> application/infrastructure; sandbox -> the same feature UI + fakes. UI contracts must not depend on adapters or host implementations.

This child retains the existing AgentFramework project. Enforce source/type boundaries now and report the remaining assembly graph separately. No new module reference, sibling change, package switch, or feature dependency in AppComponents is authorized by this plan.

Audit every type crossing a proposed public seam, including nested properties, generic arguments, enums and callbacks. AgentDefinition/AgentEditorModel/ProviderProfile/CapabilityCatalogItem should be reused only after confirming the required model graph is suitable. AgentEditorModel remains mutable and cannot be shared accidentally between sessions.

ProjectAccessListItem and SecretListItem are declared in Projects/Security implementation assemblies. A presentation-specific choice projection containing the required identity/label/permission metadata can be justified here at the adapter boundary; preserve all behavior and never copy secret values. A shared application contract relocation belongs to its owning module and needs separate concrete scope. Do not copy broad domain models merely to rename them.

Record direct and evaluated transitive project references, package-to-sibling substitutions from Directory.Build.targets, static web assets, CSS isolation, JS and Templates. Existing Conversations.Components is a local example of smaller UI composition; AppComponents is optional when the selected cluster actually needs it, not a universal base project.

Constructor-based tests must construct real new operations. If sealed Projects/Secrets/infrastructure dependencies force full-host setup, isolate the smallest genuine read/write capability or test the production adapter at the appropriate integration level. Moving an untestable dependency graph behind a single controller name is not sufficient.
