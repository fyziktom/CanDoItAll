## Durable integration boundary

Before write-side connectors land, introduce a durable boundary:

1. canonical transaction commits connector intent / operation record,
2. a worker/background job executes the side effect,
3. retries are idempotent,
4. approval state is explicit when needed,
5. UI and agents inspect operation state instead of assuming immediate success.

The existing background-job infrastructure can be reused as the execution rail. The missing part is the canonical durable intent model for connector work.
