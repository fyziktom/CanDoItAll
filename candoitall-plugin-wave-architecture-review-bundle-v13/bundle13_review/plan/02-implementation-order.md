# Implementation order

1. Repair the bundle package so the workflow can validate the bundle before and after execution.
2. Bind `AutomationRuntimeOptions` from production configuration and add the missing runtime tuning fields needed by lease recovery and worker backoff.
3. Make publish, ingress, and connector enqueue idempotency atomic under concurrent requests.
4. Move automation delivery acquisition and connector outbox acquisition to database-side lease claims.
5. Add iteration-level exception isolation and failure backoff to the hosted workers.
6. Remove production `EnqueueTrackedAsync(...)` call sites and make the legacy bridge forward queued items into the durable runtime plane.
7. Add the required tests, then rerun gates, build, and targeted suites until the bundle is green.
