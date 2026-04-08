# Required implementation evidence

- message publish path handles uniqueness races explicitly,
- ingress accept path handles uniqueness races explicitly,
- connector outbox enqueue path handles uniqueness races explicitly,
- implementation no longer depends on a naked query-then-insert pattern without conflict recovery.
