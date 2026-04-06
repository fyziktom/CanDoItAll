# Suggested sequencing

## Sequence
### Step 1
Introduce message/domain separation and multi-source signal aggregation.

### Step 2
Add canonical trigger records and Quartz bridge.

### Step 3
Add durable internal message plane and subscription registry.

### Step 4
Add hosted workers that drain:
- due trigger messages,
- connector outbox pending commands,
- queued background jobs.

### Step 5
Add plugin ingress inbox + cursor/dedupe/materialization.

### Step 6
Add observability, delivery attempts, dead-letter views, and optional MQTT bridge.

## Why this order
The scheduler should publish into a durable message plane.
The workers should drain that plane.
The ingress boundary should then reuse the same execution substrate instead of inventing another one.
