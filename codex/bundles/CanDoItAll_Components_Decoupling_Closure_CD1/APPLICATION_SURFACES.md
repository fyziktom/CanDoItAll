# Application presentation design

## Dependency and placement decision

Reuse existing light contracts and feature libraries where they fit. A small application-owned
Razor library such as `CanDoItAll.Application.UI` is a reasonable default for Dashboard,
Runtime Capabilities and safe database-profile presentation. The name and exact project split
are design choices, not an assembly quota. A feature-neutral status bar/utility primitive may
belong to existing AppComponents. Do not put application operational semantics into the generic
Components sibling or turn Configuration.UI's schema-field role into a catch-all.

The consumed renderer closure must not reference Web, Composition, Infrastructure persistence/
control-plane implementations, module implementations, provider execution or a service locator.
The host maps safe presentation values and owns effects. An opaque `object`, delegate bag or
RenderFragment containing the old native visual subtree does not remove that dependency.
A generic shell's explicitly owned contributor/effect slot remains legitimate when its actual
visual descendant is classified and independently rendered at its intended seam.

`DashboardModels.cs` currently contains pure immutable display records in the Web namespace.
Assess relocating the minimal pure contract rather than duplicating every record. Keep source,
public type and serialized compatibility where relevant; use forwarding only for a real
compatibility need. Do not move snapshot caching/loading into the renderer library. Runtime
and database native models instead usually need narrow display projections, not a new mirrored
native type system or backend contracts framework.

## Dashboard: preserve the exact product behavior

Keep `/` and `/dashboard`, their title and existing navigation semantics. Preserve all four
quick actions, the six most recently changed project cards, workflow/process activity switching,
active versus recent-fallback meaning, the five recent-run fallback, timestamps/status/lag,
observed token total, known USD cost and explicit exclusion of unknown usage/pricing.
Loading or unavailable usage is not a successful zero. Preserve old data with a visible stale
warning after refresh failure, distinct initial failure, retry and countdown.

Keep the native five-minute snapshot/cache policy, manual force-refresh and one owned timer
loop. No extra polling per child or render, no duplicated cache keyed only by public project,
no resetting the selected activity tab on refresh. Data freshness is not a navigation event.
Preserve disposal/cancellation and the profile boundary; late completions must not update a
successor opening. Keep backend authorization and pricing calculation at their existing owners.

The renderer receives a cohesive view/snapshot and typed intents or existing suitable callbacks.
Capture rendered item identities for actions. Preserve real `href` values and browser behavior
where the current UI uses links; a div with an onclick is not equivalent keyboard/navigation
behavior. Routing strings are assigned by the native mapper or a proven neutral route contract,
not reconstructed by a child guessing the parent's current selection.

Retain and update the real `HomePageTests` assertions rather than replacing them with an empty
stub-render test. Its loading-without-fake-totals, populated links, partial cost, stale snapshot,
manual and timed refresh tests form the immediate regression floor.

## Runtime Capabilities: present authority, do not own it

Keep `/settings/runtime-capabilities`, refresh and every card's readiness/support/criticality,
reason, execution boundary, implementation registration/identity/version, observation time
and remediation. Do not collapse Unsupported, Unverified, Unavailable, Misconfigured and
Available into a single boolean or infer authority from a display label.

`IHostCapabilitySnapshotProvider` and actual readiness probes stay in the native host. Map the
complete safe display meaning without taking a reference to the Composition assembly from a
reusable renderer. The actual CapabilityFact child must move/reuse with the surface; no fake
child in the sandbox. Preserve existing test IDs or an explicit reviewed selector migration.
Do not add automatic probes, startup side effects or new permission bypasses.

## Database profiles: preserve activation-for-restart semantics

The current dialog is largely presentational but its parameters directly name control-plane
models. Supply only the safe values needed to render active/pending/locked/selected profiles,
redacted target/workspace details, messages, busy/allowed actions and identity-bound callbacks.
Native services still own load, selection, activation, capability checks, reconciliation and
runtime generation. Never pass credentials or a raw connection string through a renderer,
clipboard, browser storage, data attributes, traces or screenshots.

Preserve Startup versus Runtime presentation, Continue/Close, Open Data Sources, selected
profile, Running now, Pending restart, Config lock and Activate for restart. Activation is
not a live database switch and not an automatic process restart. Changing the selection during
an operation must not redirect that operation. Double click and alternate action paths retain
owner-level admission; renderer disabled state is not backend enforcement.

The flyout must retain current safe server/database/user details, safe summary/copy controls,
recent profile choices, busy/manage guards and the full-dialog entry. The renderer must not
parse a native connection string to obtain those safe fields. Native code already computes
safe details; preserve that owner and verify the boundary with recognizable fixture secrets.

## MainLayout and status: extract markup, not the application root

Keep native MainLayout startup gating so the routed body does not initialize before the
startup choice is confirmed. Preserve `@Body`, the workspace context provider, conversation
shell host, DialogHost, notifications/tooltips and framework error/reconnect behavior. Native
Workbench state, database notifications, projects and conversation launchers remain native.

Move the real reusable utility/flyout and status markup to their chosen light leaves; do not
create a wrapper whose only purpose is to forward services back to the old Web renderer.
The top bar is already presentation-only and needs no new state engine. Preserve navigation,
chat opening, status counts, hover/focus/keyboard behavior, responsive layout and nested overlay
stacking. Error/retry presentation can be a cohesive part of the seam while its orchestration
stays native. No feature or component-type quota is imposed.

## Assets and compatibility

Follow the actual Tailwind inputs, shared CSS selectors, isolated CSS, JS imports and static
web assets of every moved surface. CSS isolation uses library-specific bundling and descendant
scope rules; a compiling move can still change styling. Verify source and published behavior,
not just file presence. Keep framework reconnect scripts and error DOM contracts unless a
specific justified move includes their real tests.

Preserve route assembly discovery, render modes, dynamic type forwards, public parameter
semantics and existing caller ownership. Avoid an HTTP client, state store, schema migration
or global dependency upgrade as a side effect of this presentation change.
