# Boundary ownership

Existing AgentFramework module remains the only changed production project.

- IProviderProfilesReads / ProviderProfilesReads: cohesive catalog, secret metadata and selected editor reads; adapter composes existing runtime/admin services. No commands.
- ProviderProfilesState / ProviderEditorSection definitions: semantic selection, explicit section, overlay visibility. No URL contract.
- ProviderProfilesSession: per-panel, directly constructed with reads; sole semantic state owner, draft/EditContext, catalog/editor loading/error, replaceable target cancellation, latest accepted generations, source-managed projection. Independent of Razor and notifications.
- AgentProviderProfilesPanel: tree/search/presentation buffers, rendering, existing commands and notifications. It delegates read selection/lifetime to session. Existing Razor/codebehind partial remains the UI form, no new partial files.

No temporary forwarding service or parallel legacy read path. Source-managed identity derives from accepted catalog and authoritative selection; a missing selected provider fails closed.
