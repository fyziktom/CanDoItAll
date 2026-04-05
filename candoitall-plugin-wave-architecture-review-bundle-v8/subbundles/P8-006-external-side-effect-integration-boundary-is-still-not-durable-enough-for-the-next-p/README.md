# P8-006 — External-side-effect integration boundary is still not durable enough for the next plugin wave

**Severity:** High  
**Hard gate:** Yes  
**Repeat offender:** Yes

## Problem
The repo has mutation coordination and rollback/compensation for some internal cross-module operations. That is better than silent partial failure, but it is not the same as a durable outbox or connector-operation boundary. Email, LinkedIn, and custom API plugins will create external side effects, retries, approval flows, and idempotency requirements that compensation alone will not make safe.

## Scope
Add a durable connector-operation boundary before write-side plugins land.

## Required direction
Before the email / LinkedIn / custom API wave lands, establish a durable connector operation boundary: canonical transaction commits intent, a worker executes connector side effects, retries are idempotent, and approval state is explicit. Internal compensation can stay where appropriate, but external side effects must not rely on same-request rollback semantics.
