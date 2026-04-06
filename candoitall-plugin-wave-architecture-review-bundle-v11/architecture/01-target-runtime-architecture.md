# Target runtime architecture for the plugin wave

## Canonical layers
### 1. Domain / user-visible artifacts
- projects
- workbench nodes and links
- resources
- prompts
- CRM / HR entities
- any explicitly materialized business artifacts

### 2. Execution plane
- trigger definitions
- internal message envelopes
- subscriptions / handler registry
- ingress envelopes
- retries / dead-letter state
- background execution workers
- execution telemetry

### 3. Runtime adapters
- Quartz-backed scheduler projection
- optional MQTT telemetry bridge
- HTTP/webhook ingress adapters
- email / WhatsApp / polling adapters

## Required rule
Domain artifacts stay canonical.
Runtime adapters stay replaceable.
The execution plane is the stable seam in the middle.

## Minimal end-to-end flow
1. A canonical trigger definition says “run every hour”.
2. The Quartz projection wakes the runtime at the right time.
3. The Quartz job publishes an internal trigger-fired message.
4. The message dispatcher routes that envelope to subscribed handlers.
5. A plugin handler fetches external input or performs analysis.
6. The handler writes durable outputs or publishes follow-up messages.
7. Only the artifacts that matter to users are materialized as Workbench nodes or other domain records.
