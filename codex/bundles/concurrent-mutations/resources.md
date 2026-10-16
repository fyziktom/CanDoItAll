# Resource ownership and cleanup

## Planned isolated PostgreSQL fixture

Reason: no `CANDOITALL_TESTS_POSTGRES_CONNECTION` is configured and no suitable isolated
test server is running. P01a fault tests and later persistence races require real
PostgreSQL 18. Reuse one fixture with the existing bounded database leases.

Before creation, reserve these exact owned names:

- Container: `candoitall-concurrent-mutations-pg18-20261016`.
- Volume: `candoitall-concurrent-mutations-pg18-20261016-data`.
- Ownership label: `candoitall.task=concurrent-mutations-20261016`.
- Image: existing `postgres:18.6-alpine`.
- Endpoint: random loopback-only published port; record actual binding after creation.
- Credentials/runtime record: ignored `.artifacts/concurrent-mutations/fixture`.
- Network: shared Docker default bridge; not owned and must not be removed.

Preserved unrelated resources observed before creation: ordinary
`candoitall-postgres18-5032` on host 5432 and `hotels-ai-content-studio` on 5534.
Do not stop, mutate or use them as fixtures.

Creation status: created container
`306f31947bed401ec0cd098e421af2aca31a88033f90df97840e33b632ed14f2`, endpoint
`127.0.0.1:50929`; owned named volume above and read-only owned password-file mount.
The ignored ownership record is `.artifacts/concurrent-mutations/fixture/ownership.json`.
Cleanup status: retained for P01a and subsequent database lanes. After the last required database lane, inspect
identity/labels/mounts/ports, remove this container and exclusively owned volume, verify
their absence and released port, then delete only the owned credential/runtime files.
The fixture is retained across related unfinished lanes, not provisioned per phase.
