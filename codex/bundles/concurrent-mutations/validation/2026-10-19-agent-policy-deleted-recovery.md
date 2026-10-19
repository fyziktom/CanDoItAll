# Native Agent policy recovery after deletion and host restart

Implements the [recovery design](../plan/P07-agent-policy-deleted-recovery.md) after signed
Process accounting checkpoint `5bf3af53f`. This closes the native deleted-Agent entry and
actual-host restart gaps left by the earlier retained-policy checkpoint. It does not
activate Process/Workflow policy execution or close the complete concurrency bundle.

## Behavior and boundaries

The interactive Agent catalog exposes **Retained policy saves** independently of live
Agent selection. The module recovery session captures the current workspace before and
after browser reads, rejects changed selections and retires on owner cancellation.
The active-profile facade forwards the lightweight context query to the existing policy
owner, which checks the actual profile generation without reading an Agent or catalog.

Web enumerates a caller/profile/scope key range in IndexedDB. At most 128 records are
accepted; the 129th causes an explicit capacity error. Only Agent ID, operation ID and
stage cross the Blazor channel. The selected full record is reread and checked against
its exact operation and stage. Historical generation is permitted for receipt lookup,
while current-context adoption still requires the actual current generation.

The existing editor restores original intent with no current editing baseline. It looks
up the original receipt, never resends a save or recreates an Agent. Accepted history
remains visible when a subsequent current read fails after deletion. Saving stays
disabled until current data is successfully read and explicitly adopted. Existing draft,
two-tab exclusion, known-outcome cleanup and explicit unsent-discard behavior remain.

UI uses shared Dialog, Stack, Alert, TextBlock, SelectionListItem and Button contracts.
The renderer performs no owner reads or writes; the module owns effects and lifetime.
No new CSS, assets, packages, project references, runtime partials or general-purpose
coordinator were introduced. The context query adds no SQL or catalog scan; list output
is bounded independently of retained payload size. These are cost bounds, not a load test.

## Validation and exact repair bridge

There are **58 distinct selected tests with latest passing evidence**, established by
the complete source bridges and exact test identities retained below. This is not one
all-pass run of all 58 tests. Earlier failures remain recorded.

| Frozen candidate suffix | Selection | Result |
| --- | --- | --- |
| `deleted-recovery` | 12 policy-state, 40 component/adapter/session, five browser cases | 54 passed, three failed |
| `deleted-recovery-ready` | One repaired component and four affected browser cases | Two passed, three failed |
| `deleted-recovery-owner` | Two affected component and three browser cases | Five passed |

The repairs address direct SelectionListItem button targeting, Playwright object-array
serialization/metadata, interactive readiness after full navigation, the missing real
active-profile forwarding method and a literal Razor error binding. The final component
selection proves actual error rendering and deleted-Agent read-only recovery. The final
browser selection proves bounded/scoped IndexedDB operations, existing two-tab/reload
behavior and the new deleted-Agent restart journey. No failed case is reclassified.
The unchanged state/other component cases, standalone visual specimen and native
editing/conflict case retain their reviewed earlier passing proof.

The real native producer commits once, loses browser acknowledgement, then the Agent is
deleted through its owner. The fixture stops only its owned application process and
starts a successor at the same origin using the same isolated database/profile and
browser storage. The catalog recovery action finds the original operation and displays
its historical RunSnapshot/three-minute policy. No live Agent form is present, no Agent
is recreated, later catalog revision remains unchanged and another save remains blocked.
Browser diagnostics are clean. List and receipt screenshots were inspected at 1920x1080;
text and footer actions are visible without horizontal overflow.

Core, UI, module, Web and selected test projects have standard Release build evidence.
Final exact discovery is two component and three browser cases. The complete portability
scan passes with **15,619 unchanged reviewed findings**, without rewriting the baseline.
Rendering register, documentation and diff checks pass. Broad Stable remains incomplete;
its historical failures and unexecuted cases are not covered by this focused checkpoint.

## Architecture gate

Status: **Pass for this recovery slice**. Baseline `snap-20261019174457-9541558e` and final
`snap-20261019182041-9541558e` retain the same three-project direction and zero cycles.
Final scope has 111 types, 1,489 members, 76 registrations, 108 findings and the same 219
workspace diagnostics. Actual affected builds pass. Two added informational member-count
findings describe the cohesive nine-member browser protocol and the 84-line recovery
session. The three-line profile-facade delegation was also inspected directly.
The existing Components assembly receives friend access for isolated session tests;
the controller is not made public. Core stays independent of Web/browser persistence.

## Evidence and resource closure

Candidates are under `.artifacts/concurrent-mutations/validation/c4-agent-policy-*`.
Final complete source pin: 9,435 files,
`0AE89946EFD493442214D528C76ECCC69929C80D2F9EE9D6C0059F62067BF74B`.
Final runtime pin: 5,019 files,
`132C2A71E1FCEE615130F21A590B8D87361EAB47D5199B4F74AA1DD45B05B0FA`.
Both inventories were verified unchanged before freeze release and documentation closure.

Compact proof under `proof/2026-10-19-planning-authority/`:

- `c4-agent-policy-deleted-recovery.json`: `61DEE081BCEA2DFD30BEE6DA106249AF598B114F5D74BD302D18F9F475A45344`.
- `c4-agent-policy-deleted-recovery-ready.json`: `496F8A8378188D7949C976C6172DDB8C2D13FF717337D7B05DEBE629BE4C81CF`.
- `c4-agent-policy-deleted-recovery-owner.json`: `0207073EE4B9BD847CA483ED3C5D76053D4BE4E2E0C51A00F6D0BEE88456C242`.

Observers cover every execution command in full. Each candidate reconciled to zero
remaining observed processes and zero database-name delta. Final execution took
2.2910436 seconds for components and 71.8415518 seconds for browser cases; the observer
covered 87.7006438 seconds. Nine final unattributed TEMP paths were preserved, not claimed
as owned cleanup. Earlier cleanup-review blockers remain separately recorded and were
not retried. The isolated PostgreSQL fixture remains shared with continuing Process work;
ordinary port 5032, its databases, installed MCP tools and SPA main were untouched.
