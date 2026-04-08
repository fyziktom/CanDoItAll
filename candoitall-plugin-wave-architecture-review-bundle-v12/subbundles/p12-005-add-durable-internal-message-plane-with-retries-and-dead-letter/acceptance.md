# Acceptance

- Add durable envelope persistence with delayed delivery, attempts, retries, and dead-letter.
- Add publisher/dispatcher abstractions.
- Add subscription registry and multi-subscriber fan-out.
- Preserve correlation id, causation id, and dedupe key.
- Recommended exact types:
  - `AutomationEnvelopeRecord`
  - `AutomationDeadLetterRecord`
  - `IAutomationMessagePublisher`
  - `IAutomationMessageDispatcher`
  - `IAutomationMessageHandler<TEnvelope>`
  - `AutomationSubscriptionRegistry`
