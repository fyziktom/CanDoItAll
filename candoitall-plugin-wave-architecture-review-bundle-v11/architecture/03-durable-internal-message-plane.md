# Durable internal message plane

## Decision
Add one durable internal message plane for commands, events, and wakeups.

## What the plane must support
- at-least-once delivery
- idempotent handlers
- retry with backoff
- dead-letter state
- correlation ID
- causation ID
- dedupe key
- delayed delivery
- fan-out to multiple subscribers
- observability of delivery history

## Recommended exact types for phase11
- `AutomationEnvelopeRecord`
- `AutomationEnvelopeState`
- `IAutomationMessagePublisher`
- `IAutomationMessageDispatcher`
- `IAutomationMessageHandler<TEnvelope>`
- `AutomationSubscriptionRegistry`
- `AutomationDeadLetterRecord`

## Required rule
The message plane is internal and operational.
It does not automatically create Workbench nodes.

## Signal aggregation rule
Replace the current singular `IAutomationSignalProvider` consumption with a composite/multi-source shape.
Recommended exact types:

- `IAutomationSignalSource`
- `CompositeAutomationSignalProvider`

This prevents last-registration-wins behavior and allows multiple modules/plugins to contribute signals safely.
