# Agent Editor Core A1 — explicit boundary, not the whole module

## In scope

Extract the actual editor-wide loading/failure state, warnings, form/shell, tabs and footer plus
four complete selected section renderers. The production host keeps effects and its existing
session; the new independent sandbox uses the same renderers.

| Section/surface | Included rendering and behavior |
|---|---|
| Shell | Loading and retry presentation, committed/pending/unknown warnings, linked-resource display, all ten stable section entries, validation, Clear/Save/Delete presentation |
| Identity | Name, role, summary, instructions, tags, avatar display/status and the existing avatar action composition point |
| Runtime | Provider/model including default/custom intent, thinking effort/support, status, workload, chat history, tool-use flag, external-call approval and confirmed auto-approval default |
| Images | Generate/store-as-project-asset flags, provider/model choice, recommended-on-save intent and policy/support guidance |
| Voice | Allow voice and stored preferred-voice override versus inherited general voice; configuration only |

Preserve existing fields and controls, not a redesigned feature. No temperature or advanced field
should be invented merely because it exists in the saved model. Preserve such values on save.
Avatar generation and SharedProviderRefreshButton remain actual host-owned integrations via
explicit, origin-bound slots/actions; provider configuration editors are not part of A1.

## Deferred, still functional in production

Memory, Project Structure Access, Workspace Tools, Secrets, Process Access and Capabilities retain
their current real content and effects. Compose them through typed section slots tied to the active
Agent editor session. Keep their current meaningful initialization/lazy-read policy, raw values
and pending child dialogs. No empty production replacements. Preserve capability assignment's
current save semantics, which may save the whole original draft. Process definition selection
currently announces unavailability; do not implement it during extraction. [R11, R12]

The sandbox may mark these sections as out of scope, but must not count their labels or placeholder
panels as extracted/rendered proof. After A1, state the remaining section/host inventory precisely.

## Suggested placement and allowed alternatives

- `src/UI/CanDoItAll.AgentFramework.Editor.UI`: real shell and four section renderers; presentation
  shapes/ports only as needed, product-neutral component dependencies.
- `src/Sandboxes/CanDoItAll.AgentFramework.Editor.UiSandbox`: deterministic no-database scenario host.
- Optional `src/Modules/CanDoItAll.Modules.AgentFramework.Editor.Contracts`: only when needed for a
  genuine common contract, never mandatory project count or duplicate whole-agent models.
- Existing product module: route/dialog consumers, session, reads/commands, provenance, policy,
  external-root/secret/Memory/capability hosts and explicit registration.

A presentation record or a cohesive view contract is acceptable. One original mutable draft and
form context stay authoritative. If supplied neutral model types can directly carry the original
draft, use them rather than synchronizing two copies. If using projections, make edits typed,
origin-bound and immediately applied through the canonical host; do not buffer a second whole form.

`AgentFramework.Models` declares references to SharedKernel and abstractions for capabilities,
Memory, Infrastructure and provider history. Existing AgentFramework.UI uses it. Evaluate transitively
and inspect instance exposure before deciding: it is not a prohibited implementation by name. [R22, R23]
`AgentFramework.Components` is broader (Core, Voice, Canvas) and is not an acceptable shortcut for
one picker. The existing model-selector facade already targets neutral Conversations rendering. [R16, R18]

A neutral thinking selector split may be justified if it preserves old callers and policy in one
place. Do not copy the MAF component wholesale into the new product leaf or route a MAF wrapper
back to the product editor. No shared-file relocation merely to make a graph smaller on paper.

## Protected graph and runtime boundaries

Before/after evaluated references and packages must prove no new Editor edge from existing
AgentFramework.UI/catalog sandbox, Projects P1/P2, Workspace leaves, Resources or Foundation/MAF
runtime. Parent product composition may depend on the new leaf. Preserve source-package substitution,
route discovery, CI test registration and static assets. Inspect public signature and hidden child
render trees, not just injected services on the top component.

One typed deferred-section slot can accept different existing host content; it may not expose
IServiceProvider or arbitrary component types selected from untrusted metadata. A provided
EditContext is a form contract, not a service locator through its Model property.

No schema, auth/approval changes, deletion protocol redesign, browser persistence of editor data,
new runtime dispatch, transport-only UI migration or new provider feature belongs here.
