# Browser and native journeys

Use actual moved renderers in both the independent host and the production Web host. Reuse
existing browser fixtures and methods; extend them rather than creating a duplicate fixture
stack. Data seeding may use native services. Distinguish a native-service action followed by
browser observation from an action actually clicked in the UI. Preserve exact evidence labels.

## J1 — independent application scenarios

Render Dashboard loading, empty, populated, partial-price and stale/error/retry states; both
activity tabs; Runtime ready/blocked/unsupported/unverified combinations and implementation
facts; database startup/runtime/config-lock/active/pending/selected states; recent flyout and
status popover. Exercise actual controls, links, clipboard adapters and full nested children.
No Web/Composition/module/runtime/database registration may be needed by this host. Independent
state instances must not share drafts/selections or echo a scripted success as native proof.

## J2 — dashboard in native Web

Enter `/` and `/dashboard` through the real startup shell using isolated data. Verify all
quick actions, populated ID-bearing links to Projects/Workflows/Live Processes, tab changes,
known cost with explicitly unknown observations and projection-lag presentation. Force a real
controlled owner read failure and prove stale data remains visible, retry recovers and loading
never fabricates zero totals. Verify exactly owned timer/refresh behavior with deterministic
native/component clock tests rather than sleeping five minutes in every browser test.

Navigate away and back; no duplicate polling/subscriptions or late update into a successor
opening. Keep authorization, current profile identity and the existing cache owner intact.

## J3 — readiness through the real shell

Open Runtime Capabilities through the actual utility action, refresh via the UI and confirm
owner-reported status/support/reason/boundary/implementation/observation/remediation. The scenario
host's synthetic statuses are separate from the real native snapshot. No extra readiness probe
or startup side effect is introduced by rendering the cards.

## J4 — profile dialog/flyout and startup

Use a specifically isolated application/control plane, never the normal 5032 instance. Before
Continue, verify the routed data owner has not initialized against a false empty database.
Verify selection versus active runtime versus pending-restart state, config lock and allowed
controls, safe current/recent details, copies, Data Sources navigation and startup/runtime modes.
With authorized isolated profile mutation, click activation once and read back the actual
pending profile; the running profile stays unchanged until its intentional isolated restart.
Where restart proof is needed use an owned host and retain the correct data-protection/workspace
identity, not a page reload described as an OS restart.

Use a recognizable synthetic fixture secret to assert it is absent from DOM/copy values/logs/
uncompressed trace content. Opening B after A, closing a pending view or selecting another
profile must not retarget A's accepted outcome or produce a duplicate activation. Exercise the
native owner and its current error/recovery behavior; don't replace it with a successful stub.

## J5 — cross-workspace shell regression

Use the source census to enumerate the actual current navigation entries and direct routes.
Exercise at least one meaningful active view and relevant deferred overlay for each family
under the new shell; reuse existing fresh/valid journeys when they already prove that combination.
The required risk paths include Workbench project/canvas and workflow/process launch dialogs,
Agent and Simple Chat/floating conversations with existing context/attachments/voice ownership,
Workspace settings/Data Sources, Projects Files, and Collaboration's actual native action.
Cover CRM/HR, Prompts, Resources, Memory, Scheduler, Plugins and TestLab navigation plus their
identified shell-sensitive dialogs. Retain current route query and back/forward/tab semantics.

A smoke navigation is not proof of a save or process execution. Select existing native save,
workflow/process accepted-run, file/chat/cancellation journeys for impacted boundaries and
check durable outcomes. Unchanged deep owner semantics may retain source-addressed PC4/prior
proof; the changed shell and overlay integration require current browser observation.

## J6 — assets, geometry and recovery

Test source and published application/scenario hosts. Check actual rendered CSS, Tailwind-only
classes, icons, clipboard, popovers, scroll ownership, focus/keyboard, z-index and nested dialogs,
including a constrained viewport as well as the established desktop viewport. Preserve route
assembly discovery and supported framework reconnect/error/reload behavior. Inspect relevant
screenshots and console/network/server logs; do not declare zero errors if expected teardown
errors are retained. Save compact proof with source tuple and redact fixture credentials.

These journeys are obligations, not six monolithic tests or a demand for six new hosts. Combine
compatible paths on a reusable isolated fixture and retain logical per-journey assertions.
