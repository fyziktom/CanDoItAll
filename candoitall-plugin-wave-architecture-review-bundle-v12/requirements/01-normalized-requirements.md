# Normalized requirements

## R12-01 — restore phase10 zero-write Workbench reads
The Workbench structure load path must be read-only again.
Projection cleanup must live behind an explicit maintenance/repair boundary.

## R12-02 — restore phase10 unknown-manifest shared editor proof
The shared connector field editor must again support unknown manifests across provider/resource pages with deterministic test hooks and secret-reference support.

## R12-03 — separate operational execution envelopes from Workbench nodes
Messages/events/commands/wakeups must not become Workbench nodes by default.
Automation signals must aggregate from multiple sources.

## R12-04 — add canonical trigger registry and Quartz-backed scheduler projection
The application must own canonical trigger definitions and project them into Quartz.
Quartz must publish durable work rather than running heavy plugin logic inline.

## R12-05 — add durable internal message plane
Commands/events/wakeups must use an application-owned durable message plane with retries, fan-out, correlation, causation, and dead-letter.

## R12-06 — add hosted runtime workers
The runtime must automatically drain due triggers, queued background work, and connector outbox pending commands.

## R12-07 — add plugin ingress inbox + cursor + explicit materialization
External sources must first land in a durable ingress inbox and only become domain artifacts through explicit materialization.

## R12-08 — add execution observability + optional MQTT telemetry bridge
Runtime execution attempts and delivery attempts must be observable.
MQTT remains optional and adapter-only.

## R12-09 — prove closure with gates and required tests
Bundle12 is not complete until the current repo has green runs of:
- phase10 gate,
- phase11 gate,
- phase12 gate.
