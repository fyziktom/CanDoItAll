# Blazor Observation Guidance

## Microsoft Learn Guidance Applied

- Use virtualization or windowing for large process/run/stage lists. Prefer `Virtualize<TItem>` with a bounded item provider when item counts can grow.
- Use `@key` on repeated process, run, stage, and timeline rows so Blazor preserves identity correctly during live updates.
- Avoid large monolithic state notifications. Split state by dashboard summaries, selected run details, dialogs, filters, and activity timeline.
- Marshal background refresh notifications into the renderer through `InvokeAsync`.
- Dispose subscriptions from state containers and timers.
- Avoid cascading values for high-frequency changing dashboard data unless the cascade is narrow and intentionally subscribed.
- Avoid excessive component instances, attribute splatting, and expensive parameter updates in hot repeated rows.
- Use `ShouldRender` only for measured expensive subtrees, not as a broad substitute for proper state shape.

## UI State Shape

Introduce a circuit-scoped state coordinator only after the observation service exists. A suitable shape is:

- `ProcessObservationDashboardState` for dashboard filters, current snapshot, refresh status, and selected focus.
- `ProcessObservationDialogState` for open descriptors and lazy payload loading.
- `ProcessObservationTimelineState` for paged timeline windows.
- `ProcessObservationRefreshCoordinator` for coalesced polling and future push-notification integration.

State containers should publish small, typed notifications such as `DashboardSnapshotChanged`, `DialogPayloadChanged`, and `ObservationErrorChanged`. Do not expose a single general `StateChanged` event for every observation change if it causes the full page to rerender.

## Refresh Strategy

Start with centralized coalesced polling because the current UI already refreshes periodically and because it is simpler to validate than a push transport. Polling should be:

- paused when no observation surface is visible
- slower for inactive processes
- cancellable when filters or selected project change
- coalesced across dashboard panels requesting the same projection
- instrumented for query duration and rendered item counts

SignalR should remain a later option for cross-browser or server-pushed dashboards. If added, send revision or invalidation notices first, then let the observation service fetch typed snapshots. Avoid pushing full high-volume snapshots through SignalR unless measurements justify it.

## Dialog Strategy

The dashboard should render buttons and detail affordances from `ProcessObservationDialogDescriptor` values. Opening a dialog loads a specific typed payload:

- run health and attempts
- stage QA and testing summary
- agent execution run details
- approval/escalation history
- artifacts and evidence
- outbox/dead-letter detail

Dialog payloads must be lazy, cancellable, and scoped to the current project and authorization context.

## AI-Driven UI Strategy

AI conversation should produce a typed observation intent, not direct component mutations. Example:

- user asks for QA detail from testing for a development process
- intent resolver creates a `ProcessObservationIntent` with focus target `QualityAssurance`, filters for process/project/run, and suggested dialog descriptors
- dashboard state applies the focus and loads the corresponding observation snapshots

The AI bridge is read-only in this bundle. Mutation commands remain outside this observation layer.
