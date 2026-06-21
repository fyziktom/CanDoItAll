# Live Processes UI/UX Parity Analysis

## Reference Branch

Reference inspected from `C:\repositories\CanDoItAll-maf-processes-refactor`.

The old Live Processes implementation had these operator affordances:

- First-tab process activity cards for live runs, escalations, and run events.
- Separate escalation cards with severity/status, primary and secondary actions.
- Escalation detail dialog with enough context to decide approval, request rework, resolve, message manager, or open the related run.
- Run detail dialog with health metrics, step health, pending approvals, rework counts, sessions, artifacts, manager chat, and recent activity.
- Active-agent/session visibility so the operator could see who was actually working.
- Process workspace operator console for approvals, rework, escalation assignment/resolution/reopen, and manual step transitions.

## Current Branch Gaps Before SB32

- Live Processes activity tab mostly showed tables/ledgers, not high-signal active cards.
- Active-agent tab had no actual agent cards from runtime claims.
- Selected time window was not authoritative because stale active runs escaped the API filter.
- Detail dialogs omitted active-agent and manager-message context.
- Start from project structure left the HR/review dialog visible during navigation.
- Desktop tabs were content-width constrained and did not use the available Live Processes width.
- Direct escalation actions from the old branch could not be ported one-for-one because `IProcessEscalationService` is not present in the current refactor.
- The current runtime contains manager recovery contracts, but no persisted `IProcessIncidentStore` or `IProcessRecoveryRequestStore` implementation is registered in the app. Adding resolve/rework buttons directly in Live Processes would therefore create UI commands without durable runtime semantics.

## SB32 Repairs

- Restored first-tab activity cards from the current projection model.
- Added active-agent cards from runtime state and step assignments.
- Marked expired leases as stale claims and excluded them from working-agent counts.
- Added detail-dialog sections for active agents, incidents, manager messages, and recent events.
- Added process-control navigation actions from run and agent cards so the operator can continue in the currently wired control surface.
- Added route-visible started feedback after launching from project structure.
- Fixed the live-window API filter and desktop tab width.

## Remaining Architectural Work

Direct `Resolve`, `Request rework`, `Approve`, and `Message manager` actions on Live Processes need a current-branch application service over the manager runtime ports. That service should own idempotency keys, incident/recovery persistence, policy results, and dispatch handoff. Reusing old `IProcessEscalationService` wholesale would cut across the refactored runtime model and would be a brittle rollback rather than a repair.
