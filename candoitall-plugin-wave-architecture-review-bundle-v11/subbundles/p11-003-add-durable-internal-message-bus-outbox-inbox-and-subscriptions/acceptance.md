# Acceptance

- Add durable internal envelope persistence with state, attempts, and delayed delivery support.
- Add publish/dispatch abstractions and a subscription registry.
- Support fan-out to multiple handlers.
- Support correlation ID, causation ID, dedupe key, retries, and dead-letter.
- Recommended exact types:
  - `AutomationEnvelopeRecord`
  - `AutomationDeadLetterRecord`
  - `IAutomationMessagePublisher`
  - `IAutomationMessageDispatcher`
  - `IAutomationMessageHandler<TEnvelope>`
  - `AutomationSubscriptionRegistry`
