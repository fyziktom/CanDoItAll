# Closure evidence checklist

- `AutomationRuntimeOptions` are bound from production configuration in non-test code.
- Sample application configuration shows the expected `Automation:Runtime` section.
- Atomic duplicate recovery is present for automation publish, ingress accept, and connector outbox enqueue.
- Automation deliveries and connector commands are acquired with database-side lease claims.
- Abandoned delivery leases can be reclaimed after timeout.
- Hosted workers isolate one iteration failure and apply failure backoff.
- Prompt Factory no longer schedules new work through the legacy queue seam.
- Legacy queue items are durably forwarded into the automation runtime when the queue is still used.
- Targeted phase13 tests, the phase13 gate, bundle validation, and solution build all pass.
