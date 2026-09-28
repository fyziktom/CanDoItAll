# Codex GPT-6 Astra Max — Collaboration UI decoupling

Implement the next bounded UI/component extraction in the main **CanDoItAll** repository: the **complete existing Collaboration workspace** at `/collaboration`, including Inbox, Threads, Escalations, quick create, thread detail/transcript, reply and mark-as-read. This is an implementation assignment, not another planning exercise. Finish the working production integration, a genuinely lightweight development sandbox, affected tests and maintained documentation. Do not proceed to another module.

The purpose is a smaller, useful UI development/watch loop. **Do not convert the product to API-only UI.** The routed production host should continue using the Collaboration application owner in-process. Keep HTTP control planes in other modules unchanged.

## 1. Authority, context and execution latitude

Use the unmodified shared foundation bundled at [`shared/`](shared/README.md), especially [`shared/prompt.md`](shared/prompt.md), its four architecture documents and [`shared/VALIDATION.md`](shared/VALIDATION.md). The repository's current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md` and current CI configuration remain authoritative. Use the current applicable CanDoItAll.SharedInfo standards. This assignment specializes the shared guidance; it does not replace it with a second general architecture.

Read [`REVIEW_NOTES.md`](REVIEW_NOTES.md) and [`VALIDATION_MATRIX.md`](VALIDATION_MATRIX.md). [`MODULE_SELECTION.md`](MODULE_SELECTION.md) explains why Collaboration was selected. The source register distinguishes freshly read files from inherited same-revision evidence. Preparation reviewed `development` at `7db3543ab437376baeca55089cb331fbe1b30483` on 2026-09-28. **That SHA is provenance, never a checkout requirement.** Work on the user's actual current checkout, including a newly created child branch from a newer development revision. Reassess relevant drift before changing code.

Record actual branch/HEAD, pre-existing changes, SDK, local dependency and asset modes, and relevant sibling revisions. Do not switch branches, reset, clean, stash or overwrite unrelated work. Do not require the worktree to be completely clean when unrelated changes can safely be preserved.

Use the available CanDoItAll Code Analytics MCP for symbol/reference and affected-test discovery, and corroborate it with source, evaluated build metadata and actual test discovery. For non-WebGL shared-component work, use the repository-required `candoitall_components` discovery/recommendation/contract workflow. If a required tool is unavailable, record the limitation and use source inspection; never invent a successful MCP call or quietly introduce a substitute component library.

Choose the smallest correct implementation. You may add the necessary lightweight projects, focused session/state helpers, tests, sandbox scenarios and narrow consumer repairs without seeking confirmation for routine choices. No quotas for interfaces, classes, partial files, components or commits; no workflow-engine conversion or micro-bundle ceremony. Implement the coherent cut rather than stopping after file movement.

Local, task-scoped **signed commits** are permitted. Keep the normal existing GPG agent/session available across commands; do not disable signing, change its security policy, store a passphrase or commit other people's changes. A locked signer blocks a commit, not independent validation. Verify signatures for commits you create. Do not push, merge, publish packages, create a PR, or release. Do not terminate the user's watch/MCP sessions or touch the existing application/database on port 5032 as a disposable test resource.

Keep source, identifiers, tests, comments, UI text, commit messages and repository documentation in English. Follow the actual repository coding style. The final owner-facing report may be Czech. This handoff package is planning material; do not blindly copy it or its audit/archive files into the application repository.

## 2. What is actually present

Inspect these current paths before editing; follow their current consumers rather than assuming the inventory is exhaustive:

- `src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor` and `.razor.cs`: the production route and the entire workspace markup/state/effects.
- `CollaborationContracts.cs`: plain request/read models and two annotated editor models, currently compiled into the implementation assembly.
- `CollaborationModels.cs`: six shared enums mixed with four EF record types and their mapping configurations.
- `CollaborationService.cs`, `.Queries.cs`, `.Commands.cs`, `.Support.cs`, registration, `Persistence/CollaborationDbContext.cs` and the module README: existing read/write ownership, canonical profile, notifications and activity mirroring.
- `src/App/CanDoItAll.Web/Components/Layout/MainLayout.State.cs` and the corresponding Collaboration shell refresh handler; MainLayout owns the Collaboration unread badge and subscribes to `CollaborationService.Changed`.
- Existing `CollaborationIntegrationTests` and `MainLayoutCollaborationTests`; verify all current references, composition/migration consumers, route discovery and tests at the actual checkout.

The production page currently injects the concrete Collaboration service and navigation, and its code-behind uses notifications. The service already owns persistence through a bounded context. **This assignment does not redo that backend ownership extraction.** The current query returns a whole workspace snapshot; it is not a paging API or a streaming conversation API.

Inventory the full rendered closure: header actions/stats, all three lists, both real forms and validation messages, transcript/status rendering, linked-context actions, empty states and the actual BaseLib children. Check route/non-route consumers, imports, CSS, Tailwind, JS and registrations. The reviewed module has no separate feature dialog or page-local JS/CSS file; recheck instead of manufacturing them to satisfy a checklist. The shell badge is a production integration consumer, not a reason to pull MainLayout or Workbench into the sandbox.

## 3. Target boundary

The default shape is:

```text
Production Web -> Modules.Collaboration [routed host + application owner]
                           |                         |
                           v                         v
                   Collaboration.UI          CollaborationDbContext
                           |
                           v
               Modules.Collaboration.Contracts

Collaboration.UiSandbox -> the SAME Collaboration.UI
                         + deterministic scenario state / intents
```

Use the established project layout unless current source provides a better existing seam:

- `src/Modules/CanDoItAll.Modules.Collaboration.Contracts`
- `src/UI/CanDoItAll.Collaboration.UI`
- `src/Sandboxes/CanDoItAll.Collaboration.UiSandbox`

These names are proposed new paths, not claims they already exist. Reuse an equivalent extraction if the checkout has advanced. Match the repository's current target framework and centralized dependency versions; the reviewed module targets `net10.0`.

### Contracts and compatibility

Move the genuinely shared, implementation-free values into the contracts assembly. Moving the existing plain `CollaborationContracts.cs` contents and the six enums is a reasonable small cut; split editor/presentation-only types differently only when it has actual boundary value. Keep persistence records, `IHasConcurrencyToken`, EF mapping/configuration types, the DbContext, transfer participant and owner services in the implementation module. Do not move `CollaborationModels.cs` wholesale to contracts.

Preserve existing CLR namespaces where practical, member shapes, validation annotations, enum numeric values **and enum member names**. EF currently persists these enums as strings. Do not introduce replacement enums with conversion glue merely to avoid the correct ownership move. Avoid cycles. Inspect schema/migration composition and serialization/public consumers after the assembly move. Source compatibility is not binary compatibility: add type forwarding only for an identified binary consumer, not as speculative infrastructure. No schema migration or altered persisted table/column/concurrency behavior is intended.

### Renderers and production host

Extract real feature rendering, not a wrapper that still renders the old heavy page. Reuse the actual BaseLib components; do not add Radzen or copy sibling component implementations. Keep routing, query-string interpretation, loaded/desired selection, application calls, user-facing notifications, navigation and effect lifetimes with the production host or its focused module-owned helpers.

Choose a presentation-plus-typed-intents seam or a cohesive host-owned workspace view contract according to this workspace's needs. Both are supported by shared v3. Two forms do not justify importing the whole CRM/HR mutation framework or creating an application-wide workspace service. A narrow testable owner port is acceptable; a service bag, `IServiceProvider`, general-purpose command executor or service locator is not.

The UI library and sandbox must not depend, directly or transitively, on the Collaboration implementation, Infrastructure, EF/Npgsql, Web/Composition, Projects/Processes implementations, schedulers, MAF runtime/provider hosts or secret stores. The host may still reference these where it genuinely owns the operation. A renderer may hold browser/render-only state; mutable business drafts must have one explicit owner and lifetime. Never leak EF records or implementation types through public component parameters, generic arguments or callbacks.

Update the product solution, affected references, imports, route discovery, maintained indexes and narrow test wiring as required. Keep runtime route discovery working with the route host remaining in its owning module. Do not add all sandbox/test projects to the production graph indiscriminately. Do not modify broad CI/package infrastructure without a concrete need.

## 4. Preserve behavior and fix the seam's concrete lifetime hazards

Use the matrix as acceptance behavior, not a class-design prescription. Separate intentional corrections to old defects from extraction regressions.

### Navigation, selection and read completion

Preserve `/collaboration` and `?threadId=<guid>`, existing item-generated links, explicit thread selection, all tab labels, and `/scheduler`/linked-context actions. Keep URLs out of the renderer's state machine. Internally identify sections by a typed value and map it explicitly to the current Tabs indices; do not create a new public tab URL contract merely for this extraction.

Separate desired route/selection from the last accepted snapshot. The current `GetWorkspaceAsync(null)` automatically selects the first inbox/thread entry; an unknown explicit ID returns no detail. Model initial-default selection, explicit-not-found and intentionally empty filtered selection separately. In particular, an unread filter must not silently reselect a read item because `null` was sent back to that fallback query. Prefer a host-side presentation/selection correction over redesigning the backend query. Same-route parameter delivery must not continually reload or reset editors just because an implicit default differs from a null query.

Keep the current policy that unread filtering applies to Inbox and Escalations, not the Threads list. Preserve the selected thread when it remains visible; use an explicit, tested transition when it no longer does. Deep links and browser navigation must work with the host's current route behavior; avoid navigation loops and duplicate loads.

Fence asynchronous results by lifetime/request generation, not only by GUID equality. Cover A -> B -> A, cancellation-ignoring old success/error/finally, and disposal. Retire/cancel and eventually dispose owned read cancellation sources correctly. A late read or command completion must not change a newer target, clear its draft, navigate it elsewhere, display a stale error as the current error, or release its busy gate.

Show real initial loading, empty, not-found, failed-load, same-scope stale data and retry states. Never represent an unavailable read as a successful empty workspace. The current owner returns one aggregate snapshot: do not invent independent server read lanes or partial availability the owner cannot establish. Preserve the last accepted same-scope snapshot after a failed refresh with explicit stale status; never show thread A as thread B.

### Drafts, validation and commands

Retain annotated validation for subject, context label/route and message body. Both submit buttons must execute the corresponding actual form validation. Preserve same-target draft and `EditContext`/validation lifetime through rerenders, refreshes and section changes. A true target switch retires the reply lifetime; do not carry A's text silently into B's reply. Choose and document a small safe target-switch policy (for example explicit discard/reset, or bounded per-target retention); do not implement a new draft-storage subsystem.

Keep quick-create Notification/Escalation preparation, reset behavior, all current context/message kinds, and the distinction between clearing reply text and resetting reply kind. Protect unsent quick-create text from unrelated thread reads. Preserve local reply semantics: the owner helper sends `MarkAsUnread: false`; manual create/automation ingress have different unread semantics. Do not make a UI-only label reinterpret those owner rules.

Capture immutable submission values, target ID and editor lifetime before dispatch. Admit one mutation per relevant editor/target action; disabled buttons are presentation, not the only gate. Do not globally lock unrelated views/forms. Cover double-click, Enter plus click, and callbacks retained from an old render. A stale callback must carry/validate its rendered target rather than acting on whatever happens to be selected later.

Choose an explicit edit-during-save policy. Disabling that form's editable fields/actions while its command is admitted is acceptable and likely simplest here; prove it and still protect target transitions. If edits remain possible, reconcile against the submitted snapshot so completion cannot erase newer text. Do not blindly reset the shared draft object after any successful asynchronous call.

### Durable write versus later effects

Continue using the real owner methods and their actual `Result` outcomes. Preserve a create's returned identity. A successful create/append/mark-read followed by a failed refresh is **saved with a refresh warning**, not a rejected write. Retry reconciliation as a read only. Prevent blind duplicate submission after a genuinely unknown result; do not invent durable idempotency guarantees or an operation receipt the existing owner does not provide.

There is a specific reviewed owner hazard: commands call `NotifyChanged()` after `SaveChangesAsync`, and `NotifyChanged` currently invokes the multicast event without isolating subscriber exceptions. A synchronous subscriber can therefore throw after persistence and prevent the success return. Reproduce this at the real owner boundary and make the smallest task-related repair so an observer failure cannot masquerade as a rejected write; log faults and preserve valid remaining observers. Inspect the touched asynchronous shell refresh path too. Do not replace the product's event infrastructure or silence arbitrary persistence exceptions.

`RecordActivitySafeAsync` already isolates activity-mirror failures. Preserve that behavior and the current ambient-transaction behavior; a successful save inside a caller-owned transaction is not proof that the outer transaction committed. The UI path should use the normal owner call, not add an ambient transaction to simplify tests.

Do not claim optimistic concurrency/conflict protection from the presence of a `ConcurrencyToken`: the module README explicitly states the current mappings do not enforce it. Test actual validation/not-found rejections and the real commit boundary. No new concurrency protocol or schema migration belongs in this UI cut.

### Security and integration

Keep backend policy at its owner and preserve current local-operator behavior; do not add authentication, tenancy or per-user read tracking. A sandbox-disabled action does not prove authorization. Assess applicability rather than inventing production capabilities. Preserve escaped message rendering and treat context routes as untrusted data; do not add raw HTML rendering or broader navigation behavior. Use existing safe-navigation facilities where available; any bounded correction must preserve legitimate existing context links and be recorded, not become a navigation redesign.

The MainLayout unread badge must continue working after create, reply and mark-read. Preserve the service lifetime and appropriate subscription cleanup. Its production-only dependency is permitted; extracting MainLayout, Workbench or Scheduler is outside this task. Validate linked navigation without launching a process, scheduler job or paid provider merely to prove a link works.

## 5. A real development sandbox and honest watch proof

Build an interactive, deterministic sandbox using the **same extracted components and real BaseLib children**. Reuse the lightweight startup and asset-mode pattern of the current Prompts/CRMHR sandboxes, not their feature dependencies. No production composition or database initialization, and no fake registrations leaking into production. Launch it on an owned free port, not the user's production/development instance.

Provide useful reachable scenarios: initial loading; empty; representative notification/system/escalation data; all three sections; unread/all; selected and missing thread; long transcript and subjects; invalid/dirty forms; admitted save; owner refusal; refresh failure with accepted stale data; committed-with-refresh-warning; delayed target transition. Forms and commands should interact with deterministic scenario state, not merely display static screenshots. Do not manufacture optimistic conflict or permissions features absent from this module.

Keep default Parity assets comparable to production. A content link to generated production CSS is an asset dependency, not a compile reference to Web. Include moved renderer sources and real children in Tailwind scanning; fail clearly if required assets are missing. Disable irrelevant repository-template copying as existing UI projects do. Reuse the current package/source and build conventions; source dependency mode and Parity/Fast asset mode are separate axes. A Fast mode is optional unless current conventions require it: add it only when it is genuinely useful and reproducible, not as another copied scaffold. Document exact generation and launch commands. Do not claim or expose a working Fast mode without its files, validation and visual proof.

Capture the evaluated transitive project/package graph and watch set with the current SDK, including imported `Directory.Build.targets` rewrites. An evaluated direct-reference list alone is not a transitive proof. Boundary tests should reject heavy edges and inappropriate public types and expose unresolved traversal edges instead of silently ignoring them. Existing CRMHR guards are useful precedent, not automatically correct templates.

Compare original Web, changed Web and the sandbox under recorded conditions. Use a comparable deterministic state and save-to-visible samples for a Razor edit, a presentation C# edit and real CSS/JS only where present. Classify Hot Reload, refresh and restart separately. Report sample count, cold/warm conditions, SDK/machine, source/sibling revisions and asset mode; do not promise a fixed speedup. The primary gain is the isolated sandbox: moving a referenced RCL does not automatically make full-Web `dotnet watch` lightweight. Do not hide source with `Watch=false` or switch to stale packages to manufacture results. Restore only your measurement edits.

## 6. Validation and completion

Follow [`VALIDATION_MATRIX.md`](VALIDATION_MATRIX.md) and current repo rules. Start with affected production builds and narrow tests, not the full suite. State source-derived expected test cases, run `--list-tests`, compare actual discovery and then execute current assemblies. Existing reviewed baselines are four `[Fact]` methods in `CanDoItAll.Tests.Integration.Runtime.CollaborationIntegrationTests` and one in `CanDoItAll.Tests.Components.Shell.MainLayoutCollaborationTests`; these are **source counts, not measured discovery or a complete impacted-test inventory**. Extend coverage and update expectations on the actual branch.

Required proof includes lightweight renderer/scenario tests, target/draft/mutation lifetime tests, real-host component integration, real-owner PostgreSQL tests, the shell badge, and Playwright against both the sandbox and production Web. For production, perform actual create, reply, mark-read, filtering and deep-link actions and verify owner read-back/restart behavior as applicable. Component-only fakes do not replace persistence proof. Use the supported large-desktop geometry, actual assets, focus/scroll and browser/server error inspection.

Use an explicitly isolated PostgreSQL 18 endpoint through the documented test variable at the reviewed revision. Record a sanitized endpoint/version, never credentials. Missing PostgreSQL, browser or provider resources block their specific proof, not independent work. Do not substitute EF InMemory and call it PostgreSQL verification, weaken required checks, or mark unavailable/skipped lanes as green.

Run portability-static and documentation gates. Widen to actual affected schema/composition/public consumers and any named broader-gate trigger; do not run all Stable, all providers, all processes or mobile layouts by habit. If current repository rules mandate a wider gate, honor it. No live LLM inference or process execution is intrinsic to this Collaboration UI cut.

Finish with a maintained module boundary/completion document under `docs/architecture`, module/UI/sandbox READMEs, navigation/testing/index updates as needed, and a concise evidence record. Do not refresh OpenAPI/SharedInfo API snapshots when no API contract changed; if a real public API change becomes unavoidable, explicitly identify it and run the appropriate contract/documentation gates.

Report: actual starting/final HEAD and signed commits; moved versus retained responsibility; intentional corrections and compatibility effects; exact builds/filters/discovery counts/results; real browser journeys; dependency/watch/asset evidence; observed performance or `not measured`; mandatory gate outcomes; and remaining debt or blocked proof. Do not call a structurally extracted but unvalidated module fully complete. No automatic next-module work, and no broad unrelated fixes hidden in this change.
