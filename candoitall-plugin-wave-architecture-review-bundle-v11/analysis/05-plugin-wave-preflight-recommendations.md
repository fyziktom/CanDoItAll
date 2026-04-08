# Plugin-wave preflight recommendations

## High-priority platform additions
### 1. Canonical trigger registry + Quartz-backed runtime scheduler
This gives the platform stable hourly/daily/cron wakeups without pushing time semantics into every plugin.

### 2. Durable internal message plane
This gives the platform a neutral transport for:

- trigger-fired wakeups,
- cross-plugin commands,
- domain events,
- approval completions,
- connector callbacks,
- agent handoffs.

### 3. Hosted execution workers
This turns pending work into actual runtime behavior.

### 4. Plugin ingress inbox
This gives polling/webhook/email/WhatsApp style plugins a safe landing zone with dedupe, cursors, and explicit materialization.

### 5. Signal aggregation instead of singular provider injection
A large plugin wave needs multiple signal contributors to coexist.

### 6. Execution policy + observability
Before agent-like plugins are added, the platform needs correlation IDs, causation IDs, retry policy, dead-letter state, and operator visibility.

## Recommendation on MQTT
MQTT is worth preparing for, but not as the canonical internal core.
Use MQTT only as an optional adapter for live dashboards, external observers, and future decomposition.
The authoritative core should remain the platform’s own durable trigger/message/inbox records.
