# In-place capability state/read seam

## Current ownership and dependencies

The existing panel's code-behind owns five injected services, catalog/editor reads, selection generations, four busy flags, assignment, verification, preview, dialogs and curator launch, plus filtering/tree/access-rule form state. Its Razor owns the complete list/detail surface. The page owns the requested route identity and existing SelectedAgentChanged/ContextAccessStateChanged contracts. AgentCapabilityList is already service-free in the broad Components assembly. That assembly already references the lightweight UI assembly; the reverse edge is forbidden.

## Target ownership

| Responsibility | Owner | Public test seam |
|---|---|---|
| Requested identity and page context | Existing AgentsHomePage | Existing callback/parameter contract |
| Accepted selection, current editor, catalog reads, load state, read cancellation/generation | Per-instance AgentCapabilitiesSession | Direct fake read port, delayed completion/cancellation/failure |
| Catalog plus selected editor reads | IAgentCapabilitiesReads / production workspace adapter | Registered adapter and real workspace composition |
| Rendering, tree expansion, search/tags/assignment/type filters, access-rule draft | AgentCapabilitiesSurface in the existing module | Immutable snapshot/selection/load state and typed intents, no feature services |
| Assignment, verification, preview, dialogs, notifications, curator launch | Existing AgentCapabilitiesPanel as effect host | Actual UI events and existing production service contracts |

Session selection is authoritative accepted state. Pending target identity is generation bookkeeping, retained for retry and fail-closed missing targets. The page's requested identity is an input; the host's parameter/callback acknowledgements are not a second selection store. The surface derives selection from parameters and never updates it locally. Loading a new target clears the prior editor before any await. Current read failure is explicit Failed; stale success/failure cannot affect a newer target. No explicit request keeps a valid current target or selects the first agent initially. Refresh of a formerly selected missing target fails closed instead of choosing another agent.

The read port has only two cohesive operations: catalog and editor reads. Session state uses an enum and generation/token; no state-class hierarchy or service bag. Immutable presentation records own collection descendants; original AgentDefinition remains in the session for the existing page callback. No mutable AgentEditorModel or access-policy transport DTO is sent to the surface. Reuse existing access-effect/scope/selector enums in a typed immutable preview draft; transport token mapping remains in the host.

## Patterns and rejected alternatives

A controlled component isolates rendering and local presentation, while a per-instance session isolates read lifetime. Extracting methods alone would leave UI tests coupled to all services. An interface per intent or presenter/controller pyramid would add no distinct ownership. Existing effects remain in one host; they do not move into a provider-style everything controller. The only new interface is the read boundary, with a production adapter and direct fake seam. No new product project or reference is introduced.

The panel retains its existing isolated CSS file. A plain scope anchor in the host and descendant selectors apply the same styles to the new surface without moving files or changing project metadata. This wrapper has no extra component/service responsibility. Future extraction must deliberately carry the surface styles; standalone styled extraction is not claimed in this child. No existing AgentCapabilityList or other feature file moves project.

## Effect compatibility and containment

Assignment still updates the current editor before Save; failed-save rollback and unknown/committed taxonomy remain explicitly deferred. Existing services still perform Save, verification, preview and dialog/chat work. Every completion is fenced by the selection generation so old work cannot publish into another selected target or clear its busy flags. Authoritative refresh goes through the session. Effects that need stronger cancellation or dialog ownership are handed off to child 02, with their current behavior characterized. No global CloseAll or generic outbox.

## Checkpoints

C00: exact existing source/consumer inventory, failing-first selected-read isolation/cancellation and failed-assignment characterization. C01: direct session tests and a surface rendered with no feature services; one host dispatch path; CSS and callback compatibility. C02: production composition/browser proof, unchanged project/routing/sibling graph, portability enforcement, no hidden mutation-hardening claim. CodeAnalytics entry owner spans the existing code-behind; its source/member inventory is supplemented with Razor and csproj reads because single-project snapshots do not prove the full MSBuild graph.
