# Native recovery after Agent deletion and host restart

Follow-on to [retained recovery](P07-agent-policy-recovery.md), after signed accounting
checkpoint `5bf3af53f`. The existing native proof covers live-Agent navigation and
browser reload. Deletion currently removes its only entry point: the editor reads a
current Agent before consulting the browser's retained submission. Host restart has
not yet been proven in the actual native browser path.

## Owner and state design

Keep one recovery action in the existing Agent catalog toolbar, independent of Agent
selection or catalog membership. A narrow module-owned dialog reads retained summaries
and opens the existing policy editor. Its UI renderer owns only rows and typed intents;
the module owns context capture, reads, selection and cancellation. No second save
implementation, UI mutation coordinator, new project reference or runtime partial.

Expose a lightweight current-policy-workspace read through the existing workspace facade
and policy owner. It validates the owner's current profile generation without reading
an Agent or loading the entire catalog. Read before and after browser enumeration; reject
a changed context. Parent profile changes cancel the owner lifetime, and late completion
must not reopen a retired dialog or publish another scope's entries.

The browser port enumerates the current caller/profile/scope only, ignoring historical
generation for address lookup. Retain the existing authenticated/trusted-local caller
hash checks before and after every JS await. Use an IndexedDB key range and at most 129
rows to enforce the existing 128-record bound; overflow or corrupt matching data is an
explicit failure, never truncation. Return only Agent ID, operation ID and stage.
Returning 128 full payloads could exceed Blazor's message limit. On selection, read the
one full record by a typed workspace/Agent address, then compare the exact operation and
stage. A changed or removed row requires refreshing the list, not opening stale data.

The original submission is not a current Agent snapshot. Bind the editor to its current
recovery context and restore the actual pending record with Original unset. It can look
up the receipt or discard a proven Prepared submission; it cannot save. A historical
acceptance remains visible after Agent deletion. Editing becomes possible only after a
successful current-state read and explicit adoption. Existing drafts and known outcomes
survive rereads, storage errors and late callbacks. Lookup never invokes PUT or recreates
an Agent. Missing/expired receipt remains unresolved.

## Composition and dependency proof

The Components MCP is configured at a missing install path. Read-only stdio queries
therefore reused the existing sibling Release binary without rebuilding, installing or
changing configuration. Both exact query children exited and their ownership/completion
records are retained in the candidate. Library discovery, concrete recommendation,
contracts, usage examples and sandbox examples were queried.

Use the existing BaseLib toolbar Button and a Medium Dialog with Stack, Alert and dense
SelectionListItem rows. The dialog body owns scrolling; the catalog remains primary.
No metrics, new packages/assets, structural CSS, primary navigation or small-screen
application behavior is introduced. Recovery opens the existing policy dialog after
closing the list, so overlays have one clear owner at a time. Validate at 1920x1080.

CodeAnalytics baseline `snap-20261019174457-9541558e` covers UI Catalog, native component
hosts and Web Infrastructure: three projects, 104 types, 1,453 members, 76 registrations,
106 findings, 219 diagnostics and zero cycles. The small Core context query remains in
the existing policy owner/facade; inspect and build it directly. Compare the same UI
scope after implementation and retain actual build evidence for workspace diagnostics.

## Host restart and proof

Extend the existing owned Playwright fixture to restart only its own application process
while retaining the same isolated database, profile, origin, browser context and storage.
Never use or restart the ordinary application on 5032. Preserve exact process ownership,
serialized lifecycle and cleanup of the successor. Reuse the existing production build;
do not create another stack or a bundle-specific build configuration.

Prove bounded scoped enumeration, caller change during await, foreign/malformed rows,
deleted-Agent entry, stale list selection, retained draft/receipt separation, scope
retirement and unknown receipt behavior. The actual native producer must retain before
dispatch, commit once, lose browser acknowledgement, then recover after deletion and an
actual host restart at the same origin. Assert original historical values, no second
write, no recreated Agent, unchanged later catalog state and clean browser diagnostics.
Keep existing two-tab/reload proof in the affected regression selection. Confirm exact
discovery, required builds/static gates, complete resource observation, final visual
inspection and signed documentation closure before claiming this native gap closed.

## Implementation review before runtime proof

Snapshot `snap-20261019180127-9541558e` retains the same three-project direction and zero
cycles: 111 types, 1,489 members, 76 registrations, 108 findings and the same 219 workspace
diagnostics. Two added informational member-count findings describe the nine-member
browser retention protocol and the ten-member, 84-line recovery session. Both remain
cohesive; neither introduces a dependency or service locator. No project references,
new runtime partials, provider calls, source copies or package changes were introduced.
The later test-friend assembly attribute exposes internals only to the existing Components
test assembly; production API and dependency direction stay unchanged.

The session can be tested directly through that friend boundary and through the native
dialog. The rendering register reviews four changed inputs and the two new components.
Core, UI, module and Web production builds passed before the selected runtime proof.
The first Components build identified a missing test-friend declaration; it was added
instead of making the controller public. Test results and final closure are recorded
separately; these source observations do not establish runtime success.

## Validated closure

The [final recovery record](../validation/2026-10-19-agent-policy-deleted-recovery.md)
retains the exact failed candidates, repairs and 58 latest passing selected identities.
Native deletion and an actual owned-host restart now recover the original receipt through
the real active-profile facade. The final snapshot is `snap-20261019182041-9541558e`,
with the same counts and dependency direction as the implementation review. The browser
producer, read-only receipt lookup, real facade, rendering, profile retirement and
resource cleanup now support the same narrow closure claim. Wider R5/R6 work remains.
