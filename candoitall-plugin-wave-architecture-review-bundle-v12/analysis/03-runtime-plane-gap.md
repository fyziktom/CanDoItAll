# Runtime-plane gap analysis

## Current baseline
The repo already has:
- a UI-visible `BackgroundJobRecord` tracker,
- an in-memory `IBackgroundJobQueue`,
- a connector command outbox with retry / dead-letter semantics for connector commands.

That is useful groundwork, but it is **not** the plugin runtime baseline required by bundle11.

## What is still missing

### 1. Execution-plane separation
There is still no generic operational execution envelope model that is clearly separate from `ProjectObjectRecord`.
Messages, wakeups, retries, and pub-sub coordination still have no canonical home.

### 2. Multi-source automation signals
`AutomationWorkspaceService` still consumes a singular `IAutomationSignalProvider`.
That is a last-registration-wins shape and is not open-world enough for multiple plugins/modules.

### 3. Canonical scheduling
There is no canonical trigger registry, no trigger persistence model, and no Quartz bridge.
Plugins that need cron/hourly/daily wakeups would still have to invent their own mechanics.

### 4. Durable internal messaging
There is no durable application-owned message plane for commands/events/wakeups with:
- fan-out,
- correlation + causation,
- retries,
- dead-letter,
- restart-safe delivery.

### 5. Hosted runtime workers
`ProcessPendingAsync(...)` exists for connector commands, but there is no hosted worker that calls it automatically.
`IBackgroundJobQueue.DequeueAsync(...)` exists, but no visible runtime consumer exists.
There are no hosted workers for due triggers either.

### 6. Plugin ingress boundary
There is no generic ingress inbox for external envelopes from email, WhatsApp, webhooks, or polling connectors.
There is no cursor store and no explicit materialization boundary.

### 7. Runtime observability
There is no generic execution log / delivery attempt store for internal runtime work.
Connector outbox audit is useful but too local to substitute for platform-level runtime observability.

## Consequence
The repo is still missing the common execution substrate that would let new plugins behave like first-class platform citizens instead of one-off implementations.
