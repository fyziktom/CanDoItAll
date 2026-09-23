# UI component seams

How this product separates a feature's rendering from the host that owns its state, its
reads and its writes. The rules below are the ones the completed extractions actually
established and proved: the Agents workspace, the Prompt Gallery and the seven CRM / HR
workspaces. Each rule names the evidence that keeps it true.

This document is the canonical product guidance for that work. Family-wide conventions that
are not specific to this repository live in `CanDoItAll.SharedInfo`; keep repository-specific
scanners, baselines and test commands here.

## Placement

| Location | Owns | Excludes |
|---|---|---|
| `CanDoItAll.Components.*`, `CanDoItAll.FileTools.*` (sibling repositories) | Product-neutral reusable capabilities | Application feature semantics |
| `src/UI/CanDoItAll.AppComponents` | Application-wide, feature-neutral shell, navigation, overlays and host adaptation | Concrete module implementations |
| `src/UI/CanDoItAll.AppComponents.RecordBrowsing` | The shared paged record browser, picker and selection family | Feature semantics of any one module |
| A feature rendering library under `src/UI` (`CanDoItAll.CrmHr.UI`, `CanDoItAll.Prompts.UI`) | A feature's renderers, its presentation types and the read ports they need | Persistence, provider implementations, Web composition |
| A feature contracts assembly under `src/Modules` (`CanDoItAll.Modules.CrmHr.Contracts`, `CanDoItAll.Modules.Prompts.Contracts`, `CanDoItAll.Modules.Projects.Contracts`) | Stable feature models and enumerations the renderers and the owners share | Host-specific rendering |
| A module under `src/Modules` | Routed hosts, owners, mutations, adapters and registrations | Reusable feature rendering logic |

Reuse by several modules does not move ownership: `CanDoItAll.Modules.Projects.Contracts`
exists so the CRM / HR renderers can name a project without referencing the Projects module
implementation. A dependency from `AppComponents` to a concrete feature module stays
forbidden in both directions of reasoning: the shell never learns a feature's semantics.

Placement includes scoped CSS, static web assets, JavaScript modules, Tailwind inputs and
assembly route discovery. Moving `.razor` files without moving those responsibilities is an
unfinished move.

## The seam

A routed page owns state, reads, mutations, navigation, agent context and effect lifetimes.
A renderer owns markup and the events it raises. Between them sits one explicit contract.

Two shapes are in use, and both are correct where they are used:

- **Presentation record.** The host maps its models into an immutable record and the
  renderer binds to it. Used where the surface is a read view with a few intents, such as
  `CrmHrActivitySurface` binding `CrmHrActivityPresentation`. An intent carries the record
  the operator acted on, not an index, so a stale callback cannot act on a different row.
- **View contract.** The host implements `ICrmHr<Area>WorkspaceView` and the surface binds
  to it directly. Used where a workspace has many editors, dialogs and actions over one
  record, where a record-per-render would be a second copy of the whole page. The surface
  injects read ports only; every mutation is a method on the contract.

Neither is a universal template. Choose the record when the renderer only presents; choose
the view contract when the renderer is a workspace whose state the host must keep owning.

Children receive typed values and raise callbacks or a cohesive intent family. They do not
parse or build their parent's URL, and a section is identified by a typed value with an
explicit token map, never by a numeric index, a localized label or `enum.ToString()` as an
external contract.

## State and intent

| State | Owner |
|---|---|
| Durable selection, meaningful view, committed filter | The routed host, and the route where it is route-significant |
| Active editor or overlay target and section | The routed host, separately from list selection |
| Mutable draft, validation and `EditContext` | The editor draft instance the host holds |
| Loaded data, its loading and failure state | A read session keyed to its request |
| Busy flags, request generations, focus | Transient host state |
| Confidential content | Never in navigation state |
| Scenario selection | The development sandbox only |

One authority per state, not one large record and not one large class. A host may mirror an
input for compatibility, but an effect host never becomes a second authority for a selection
the route owns. Mutable drafts and request generations never go into serializable navigation
state.

## Edit and effect lifetime

Before a renderer is extracted, the host must be able to answer all of these:

- Same record, different section: the draft, its `EditContext` and its raw validation state
  survive; only the lazy data that section needs is loaded.
- A different record, a create, or a cleared selection: an explicit transition that retires
  the draft.
- A save: the durable identity and expected version are updated without resetting unrelated
  editors.
- Close or delete: the matching active request is cleared and unrelated presentations stay.
- Request A superseded by B: a late result, error, notification or completion from A can
  never update B or reopen a dismissed session.
- Dispose: owned reads are cancelled and subscriptions detached; cancellation never undoes
  committed work.

`CrmHrMutationGate` admits one write per editor lifetime, so a double dispatch, Enter on the
form and an alternate action of the same form cannot produce two writes. A retired lifetime
never blocks its successor.

Nested dialogs belong to the lifetime that opened them. Closing an owner removes only its own
presentations; a global close-all is not an ownership boundary.

## Mutation outcomes

Record the outcome at the owner's real commit boundary, and prove all four:

| Outcome | Required treatment |
|---|---|
| Known rejected before the write | The draft is preserved for correction, and a concurrency conflict is distinguishable from a validation refusal |
| Known committed | The returned identity is preserved and the completion is published once |
| Committed with a secondary warning | The identity and a visible warning are retained; reconciliation retries reads only and never replays the write |
| Genuinely unknown result | The draft is preserved, a blind replay is prevented and the operator has an explicit recovery path |

A cancelled view does not prove the command did not commit, and a general catch of
persistence exceptions is not an "unknown" classification when the owner already returns a
deterministic rejection or a commit identity.

## Reconciliation after a commit

The submission a host dispatches is the operation's origin and is retained separately from
the live draft, because the operator keeps typing while the write is in flight.
`CrmHrDraftReconciler` holds it:

- The commit's own read-back compares the live draft with what was **submitted**, not with
  the values the draft started from.
- Nothing typed after the dispatch: the owner's accepted values start a fresh draft.
- Something typed after the dispatch: the draft instance and its `EditContext` survive, the
  fields typed afterwards keep the operator's text and every other field takes what the
  owner accepted, including an identity the owner assigned and the version it returned.
- A list draft is reconciled row by row, paired by the identity fields the caller names, so
  a row that commit created adopts its owner identity instead of being deleted and recreated
  by the next save.
- A rejected or retired write leaves no submission behind and can never be read as a commit.

A create editor is different: it starts over when its write is accepted, unless the operator
has already typed the next record into it.

## Independent read lanes

Aggregate availability, selection access and independently resolved action targets are
separate decisions. A region keeps its own loading, failure and retry state and does not
force its neighbours to reload. An accepted snapshot that is not available is reported as
unavailable rather than as zero, and a refresh failure leaves the previously accepted data
visible with explicit stale state. A desired scope never inherits another scope's cached
values.

`CrmHrActivityHistorySession` is the shape in use: a busy gate, a generation fence and a
per-read cancellation token disposed when the read returns.

## Context and authorization

Backend policy stays at the backend boundary. A disabled control is presentation, not
enforcement. Capability assignment, allowed source reads, trusted project context, runtime
admission and approval are separate prerequisites; an ordinary planner does not gain
administration because a test needed it.

A write that targets another module's aggregate carries the admission its load captured:
`ProjectWriteAdmission` names the project lifetime the editor saw, and a project retired and
recreated under the same public identity is refused rather than rebound.

## Assets

A feature UI library references only the libraries it actually uses. Scoped CSS, JavaScript
modules and static web assets move with the components that need them, and a host registers
what those components require (`AddCanDoItAllCharts()` for the chart family, for example).
The sibling source mode in `Directory.Build.targets` turns every `CanDoItAll.Components.*`
package reference into a project reference to the sibling checkout; CI pins the exact sibling
commit it consumes.

## Scenario host and production proof

Every extracted surface has a scenario in the feature's UI sandbox, and the sandbox renders
the same components as production. A sandbox must not need the module implementations, the
production runtime registrations or a database to render and edit a deterministic scenario.

Scenarios cover at least: loading, empty, realistic and large data, saved but unavailable
references, core and partial failures, retry, a selected record, a non-default section, the
relevant open overlays and a restricted state. A stub child is not a scenario.

Proof layers do not substitute for each other:

| Layer | Proves |
|---|---|
| Unit | Deterministic policy and state transitions |
| Components (bUnit over the real hosts and PostgreSQL) | Rendering, interaction and host lifetime through the public seams |
| Integration | Real owner semantics, persistence and HTTP contracts |
| Playwright on the real Web host | Assets, focus, stacking, geometry and the operator's actual journey |

A fake that echoes the desired result proves only its own layer.

## Measurement

Record the loop before a move and measure the same scenarios afterwards: host and SDK,
source and sibling revisions, the evaluated project graph, the watch file set, startup and
edit-to-visible-change for Razor, C#, scoped CSS and JavaScript. Report the conditions and
the sample count. A single observation is a single observation; it is not a speedup claim.

## Anti-patterns

Reject a wrapper whose layers only forward; a service-bag facade or a hidden
`IServiceProvider`; a route page accumulating unrelated effects; an interface quota or a
mandatory interface for every helper; a DTO copy with no boundary value; a parent-only
injection check presented as sandbox proof; mutable session data shared through
circuit-scoped services; a section change that recreates an editor and loses its draft; one
aggregate that destroys independent loading and failure boundaries; partial-file growth used
as separation; and tests that assert file counts, partial-class counts or interface quotas.

Keep a component local when its behaviour is rendering, element references, focus or
transient interaction. Extract policy, external operations, repeated workflow or duplicated
semantic ownership. A thin host is legitimate when it owns dialog or session lifetime,
composition, focus and result adaptation, or a real route boundary.

## Worked examples

| Example | What it shows | Its boundary |
|---|---|---|
| [CRM / HR](crm-hr-ui-completion.md) | Seven routed workspaces behind view contracts, host-owned slots, the mutation gate, the draft reconciler, a backend-free sandbox and browser journeys | A whole module with many editors over one record; a small read panel does not need this much machinery |
| [CRM / HR Home, account summary and activity](crm-hr-home-ui-boundary.md), [Financials](crm-hr-financials-ui-boundary.md) | Immutable presentation records, rendered-origin intents, an interactivity signal, and a chart whose sparse currencies stay separate | Read surfaces with few intents |
| [Prompt Gallery](prompt-gallery-ui-boundary.md) | A contracts assembly, a renderer library, module sessions and hosts, and a target-bound picker | A catalog and its editor, not a record workspace |
| [Agent tool failure recovery](agent-tool-failure-recovery-boundary.md) | Effect ownership and recovery across an agent runtime boundary | Runtime effects, not form editing |

## Traps this product has actually hit

- A `@for` loop variable read inside component child content is evaluated after the loop:
  copy it per row and remove a row by its instance, never by a position a later edit can shift.
- Two dialogs opened by one render race on their JavaScript module import; a nested dialog
  needs its owner to re-raise descendants after `showModal()`.
- A footer Save that sits outside its `<form>` must validate the same form; use the
  workspace surface's `ContextFor`/`SubmitAsync` rather than relying on the HTML `form`
  attribute, which a rendered-component test does not follow.
- `WaitForAssertion` runs on the renderer dispatcher: blocking on an owner read inside it
  deadlocks the test host. Read owners after the interaction returns.
- A rendered element is not there the moment its host's state changes; wait for the element,
  not for the state, before driving it.
