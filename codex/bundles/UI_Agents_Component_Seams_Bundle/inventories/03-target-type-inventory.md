# Candidate target contracts and type audit

Names illustrate cohesive roles, not required spellings or an interface quota. Keep public compatibility and document chosen ownership/production callers.

| Candidate family | Meaning / constraints |
|---|---|
| AgentWorkspaceSection / AgentsWorkspaceState | Typed semantic workspace section/selection/context, excluding mutable editor draft |
| AgentDetailsSection | Stable section identity mapped explicitly to existing visual order |
| AgentDetailsRequest / editor target identity | Existing/create target plus editor-instance distinction; separate from catalog selection |
| Overview/usage view results and query operations | Preserve demand/load regions; never require history-host eager aggregation |
| AgentCatalogSnapshot / view state / intents | Controlled catalog data and selected identities, typed actions; UI-local transient state remains local |
| Catalog operations / host coordination | Real application workflows separate from dialog/chat presentation; test normal constructors |
| AgentEditorSession / load request | One owned mutable draft/edit context/version and reference regions; explicit lifecycle/generation |
| Editor command inputs/outcomes | Typed save/delete/capability outcomes, including commit versus refresh failure and returned identity/version |
| Pure editor policies | Normalization, permission mapping and managed identity rules where non-trivial |
| Narrow reference projections / ports | Only when a meaningful boundary or implementation-owned DTO dependency justifies it |

Reuse AgentDefinition, AgentEditorModel, ProviderProfile and CapabilityCatalogItem where their complete type/assembly graph is suitable. Do not assume a type is lightweight because its name says model. Audit nested fields and protect sensitive metadata.

ProjectAccessListItem and SecretListItem live in Projects/Security implementation assemblies. A narrow UI read projection can avoid importing that assembly into a future feature UI project; record required fields and round-trip meaning. Moving shared contracts across modules is separately owned work. Do not duplicate complete domain models just for stylistic symmetry.

InitialSession is not a required production parameter. Use the production loading seam in tests/sandboxes by default. Do not expose private fields, numeric tab state, dictionaries, service bags or controllers that retain circuit-wide draft state.
