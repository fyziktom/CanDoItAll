# P13-002 — Make runtime idempotency atomic under concurrency

Sequential deduplication is present today, but it is implemented as read-then-insert. That is not safe enough for parallel workers or future multi-instance runtime topologies.
