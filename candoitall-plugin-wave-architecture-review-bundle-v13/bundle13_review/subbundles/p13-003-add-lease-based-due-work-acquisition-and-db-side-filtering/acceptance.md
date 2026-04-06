# Acceptance

- Acquire due automation deliveries at the database boundary.
- Acquire due connector outbox commands at the database boundary.
- Introduce a real claim/lease protocol so parallel workers do not process the same unit of work twice.
- Recover abandoned leases after a timeout.
- Remove broad in-memory candidate scans from hot worker paths.
