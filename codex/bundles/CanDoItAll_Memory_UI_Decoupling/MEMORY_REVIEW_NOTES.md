# Memory: source map, target architecture and implementation consequences

## Why this slice now

Memory is a cohesive next workspace after Scheduler: a single routed page with
seven defined areas and existing responsibility-specific application owners. It
is not a proposal to implement the Memory runtime or all possible provider
capabilities. Processes and Workbench remain later work. Workspace/Projects/Resources
are not authorized here. This recommendation is based on the inspected ownership
and dependency boundaries, not a measured ranking of every remaining module. [EV10, ME01–ME03, ME22]

The relevant source is the implementation SHA recorded in [SOURCES.md](SOURCES.md).
No `Memory.UI` or Memory sandbox was introduced by the reviewed Scheduler commit;
verify the actual checkout before creating projects.

## Present topology

```text
MemoryProvidersPage.razor
  -> transient MemoryProvidersPageController
  -> IMemoryProviderManagementUiService
     -> profile / query / ledger action / ingestion / snapshot services
        -> real profile/ledger stores and provider-neutral operation handler
  -> module-local reusable renderers
     -> UI records, editor models, policies, dynamic-provider UI projection
```

The current module project references BaseLib/Common, SharedKernel and
Memory.Abstractions/Application/Http/Mcp. It does not itself declare an EF package.
Nevertheless, a renderer built from that module remains coupled to application and
transport implementations. The solution is a real assembly boundary, not a claim
that every current reference is equally expensive. [ME15, ME19]

## Proposed ownership

| Responsibility | Destination/decision |
| --- | --- |
| `/memory`, composition, navigation, profile lifetime and concrete owners | Existing module |
| Generic seven-tab workspace and its actual components | `CanDoItAll.Memory.UI` |
| Pure shared editor/value contracts | Small feature contracts project or existing suitable abstractions |
| Stateful presentation, reads and immutable submission handling | Page-owned session; optional reusable Presentation project only with real dual-host use |
| HTTP/MCP configuration mapping, driver construction, operation handling and stores | Existing owner implementations |
| Approved dynamic provider UI | Explicit registered extension/host projection; no arbitrary assembly loading |
| Sandbox state | Deterministic owner substitute with the same presentation policy and real renderers |

Do not push product-specific types into a general shared Components project. Do not
pull the concrete Memory module back through AppComponents or a convenience
`FromProfile` method on a supposedly light type. Preserve any existing public API
/configuration consumers when relocating types.

### Models that require an actual seam decision

`MemoryProviderProfileEditorModel` uses driver constants, static mapping and nested
mutable HTTP/MCP models. It also carries preserved manifest metadata. Simply copying
it to a contracts folder without inspecting these references does not decouple it.
Separate data from transport mapping; preserve defaults with explicit compatibility
checks rather than a copied collection of magic protocol strings. [ME10]

`MemoryProviderOperationUiModels` uses Application statuses. An exhaustive bounded
UI projection is legitimate when it avoids importing application orchestration;
a value relocation is also legitimate if it preserves real consumers and introduces
no circular reference. Keep accepted, completed, rejected, unknown, dispatched,
unmatched and unsupported states distinct. Do not use a universal Boolean result
or broadly relocate the operation-handler implementation. [ME14, ME19]

Safe profile round trips include unknown extension JsonElements, UI surfaces,
capabilities, limits, protocol and interaction metadata. HTTP and NativeRemote use
their current credential-reference conventions; MCP references the complete auth
header environment value. Legacy raw credentials must not leak back into UI or
persistence. Existing round-trip tests are valuable behavioral invariants, not
permission to serialize whole editor trees on every render. [ME10, ME20]

## Current controller transitions to improve during extraction

### Refresh is destructive to draft identity

`RefreshCoreAsync` always builds a new profile editor from the selected snapshot.
It is called after queries and ledger actions as well as explicit refresh/save.
Thus an unrelated action can discard a dirty transport/profile draft. The tab
render key changes with counts and may unmount active controls. Preserve raw text,
validation and focus where the logical editor survives; do not merely retain the
last blurred model value. [ME01, ME02]

### Selection and action result ownership are not fenced

`SelectProviderAsync` changes an ID and awaits a snapshot; later results assign the
selected ID and editor without an original-request check. A query result also
sets feedback context and forces tab 4 after its refresh. A stale A query can thus
publish into a page the user has already moved to B. Use a concrete provider/draft
origin, complete request snapshot and view-intent generation; current selection is
not an output destination. [ME02]

The snapshot reader defaults to the first provider when an explicit requested ID
is absent. Preserve initial selection behavior where useful, but distinguish an
explicit missing target at the UI boundary. Never execute a follow-up against the
fallback provider just because an old snapshot returned it. The list should remain
usable in missing/empty states, as the Plugins repair now demonstrates. [ME04, PL01]

### Mutable requests cross a guard await

The query service chooses its required capability and awaits the action guard,
then reads the editor's query/provenance. Feedback similarly reads more editor
fields after its guard. Detach the complete input at entry so one operation cannot
combine the old capability decision with the user's newer payload. Include nested
profile fields, tags and preserved extension objects in snapshot tests. [ME06, ME10, ME12]

### Busy state is not admission

`ExecuteAsync` accepts another action and only toggles a Boolean. An older finally
can unlock a newer request. Define same-origin conflicts in code and independently
verify dispatch counts, not only disabled attributes. Keep unrelated read regions
usable rather than replacing the entire page with a global spinner. [ME02]

### Accepted work is not a refresh

A profile upsert returns its actual identity. A query can return an operation,
accepted handle, context pack, feedback handle and dispatch-attempt evidence. Retain
those before any secondary snapshot read. A failed read is not evidence of a failed
write and a query may contact an external provider or create ledger state. [ME05, ME12, ME14]

Explicit operation-status refresh is an operation-handler request; page Refresh
is a store/snapshot read. Preserve the distinction. The demo action creates up to
two fixed IDs sequentially, so an intermediate failure must not imply all-or-none
creation or silently recreate confirmed records. [ME04–ME06]

## Complete seven-tab behavior without scope inflation

| Tab | Required production behavior |
| --- | --- |
| Providers | Empty/loading/stale/selected states; profile configuration; current executable driver choices and validation; explicit demos, no automatic seeding |
| Operations | Real ledger identity/status/provenance; MCP status action where actually supported; cancellation unavailable for current drivers |
| Events | Existing inbox presentation; declared claims do not enable unsupported acknowledgement |
| Feedback | Existing ledger and matched/unmatched facts; no invented trusted context delivery or unsupported execution |
| Query | Real Mock/HTTP/NativeRemote sync query and MCP supported modes; exact result/provider origin; meaningful failed/accepted/unknown presentation |
| Ingestion | Existing unavailable/editor/result presentation according to shipped policy; no new driver/worker feature |
| Provider UI | Registered RCL, safe iframe/external URL and explicit blocked/missing conditions |

The current executable capability policy allows no shipped ingestion, feedback,
provider-push acknowledgement or cancellation. The UI must not advertise success
for these based solely on a manifest. The guard must still reject direct invocation.
The backend's current async capability and MCP tool requirements remain intact.
This matters more than obtaining a green click path for every visible section.
[ME09, ME11, ME21]

The dormant manual ingestion method performs a ledger read after enqueue returns
IDs. Preserve that ordering's evidence when adapting outcomes, but do not use it as
an excuse to enable the unreachable action or create a new distributed receipt
protocol. Synthetic accepted/result fixtures test display only. [ME13]

## Dynamic Provider UI and security

The projector gates enabled/healthy provider, declared capability, registered RCL
key, and URL policy. URLs reject userinfo, query and fragment and permit HTTPS or
loopback HTTP. The renderer consumes the approved result and passes the current
Provider and Surface parameters into DynamicComponent. [ME07, ME08, ME18]

Keep the built-in mock panel and intended extension contract. A local fixture RCL
may verify component parameters and disposal; it is not a substitute for checking
the actual production registration and the real host's selection behavior. Avoid
capturing a heavy owner in a RenderFragment hidden behind an otherwise light type.
If a host slot is used, explicitly test its production composition and base sandbox
independently. Do not claim every possible third-party provider UI has a light graph.

Keep iframe and external link protections unchanged or narrowly strengthened if a
concrete introduced regression requires it. Never permit arbitrary HTML/script,
unsafe URLs or assembly activation from user-configured text. Do not log query
contents, tokens, credential values, full sensitive URLs or unredacted provider
exceptions in generic notices. Test with harmless fixtures, not real accounts.

## Tests and consumers to locate locally

Known existing component families include `MemoryProvidersPageTests`,
`MemoryProviderOperationsPageTests`, `MemoryProviderUiSurfacePageTests`,
`MemoryProviderProfileEditorComponentTests`, `MemoryProviderProfileEditorRoundTripTests`,
`MemoryProviderProfileEditorValidationTests` and `MemoryUiRefactoringCheckpointTests`.
Some are named in historical/source inventory rather than fully read in this review.
Locate the exact current files, categories, helper ownership and assembly membership;
do not assign an expected discovery count from this paragraph.

Follow references from profile configuration contracts and editor mappers into APIs
and alternative-UI consumers. Map `MemoryOperationHandlerStatus`, `MemoryLedgerStatus`
and provider capability policy consumers before moving public types. Use the Memory
test solution for the affected runtime/driver contracts rather than defaulting to
all tests or native Cognitive Memory tests outside this repository.

The known round-trip tests and the loading/zero/guard tests were inspected as source
only. Preserve their assertions. If brittle source-path/line-count checks conflict
with legitimate placement, replace that structural assertion with the intended
responsibility/dependency proof; do not delete the accompanying behavior coverage.
[ME20, ME21]

## Risks and performance controls

Measure the real evaluated graph, not namespaces. Data-only DTO mapping must not
copy the complete profile/catalog per keystroke. Keep independent lane cancellation
and draft/cache retention bounded and per page. Passive tab/input changes should
not call provider health/query/status ports. Inspect actual ledger-store defaults
before changing read limits; the reviewed snapshot reader does not itself specify
all limits. Report existing limits honestly instead of silently changing pagination.

Move the Memory page's scoped CSS with its consuming DOM/descendants, maintain the
authoritative theme link, and inspect source and published standalone assets.
Controls rendered through a third-party RCL or iframe must be included in the
appropriate host/asset proof. Large scenarios are bounded development fixtures,
not a reason to scan all history or initialize provider runtimes in the sandbox.

See [DEV_LOOP.md](DEV_LOOP.md) for the measurement procedure and
[VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) for closure criteria.
