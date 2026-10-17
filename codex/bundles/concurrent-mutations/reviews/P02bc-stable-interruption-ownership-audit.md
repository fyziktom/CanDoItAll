# Interrupted Stable Integration: exact residue ownership

Read-only audit recorded at 2026-10-16T21:55:00Z. **No deletion, database command,
process stop or secret-content read was performed. Cleanup remains parent-owned.**

Cleanup blocker: the parent's guarded command to drop this exact leased database
and remove this exact directory was rejected **before launch** by automatic approval
review with the stated reason `blocked by policy`. No resource was removed and that
command created no ownership record. The parent will retain ignored provenance
separately. Do not retry the drop/delete through another route. Both exact residues
remain outstanding; the existing isolated fixture is retained/reused, and cleanup
must not be reported successful.

The retained main-checkout proof is
`.artifacts/concurrent-mutations/validation/p02bc-stable-qualified/`.
`execute-CanDoItAll.Tests.Integration.command.json` binds the original runner 49540,
child 3192, standard Release Integration command and its start at 20:30:50Z.
`integration-owned-interruption.json` binds stopped testhost 49080, parent 36352,
testhost start 20:30:51Z and interruption 21:39:55Z. Both retained stack captures show
the same receipt-provider DI wait. The partial result remains incomplete, not green.

## Positive resource binding

- Fixture manifest `.artifacts/concurrent-mutations/fixture/ownership.json` records
  container `candoitall-concurrent-mutations-pg18-20261016`, ID
  `306f31947bed401ec0cd098e421af2aca31a88033f90df97840e33b632ed14f2`, its same-name
  `-data` volume, bundle label `concurrent-mutations-20261016` and endpoint
  `127.0.0.1:60920`, PostgreSQL role `postgres`.
- Parent reported one remaining leased database:
  `cditall_primary_67c47aa1d495479495c55deaa4fdeedf`, with zero clients; this audit did
  not independently query PostgreSQL.
- Exact retained directory:
  `C:\Users\lucys\AppData\Local\Temp\integration-agentframework-structured-output-continuation-failure\3bdf069eb3754e4894a4b9e77a186edd`.
- Its activation record is
  `control-plane/database-profiles/dataset-activations/fe1dc186-8182-7f77-e946-9c63114a51ff.json`.
  Profile ID is `fe1dc186-8182-7f77-e946-9c63114a51ff`, namespace is
  `45e56c76-85ce-4238-9326-5727211c2d84`, and maintenance ID is
  `b87fdf8b-7040-492f-b723-ae24da6040a5`. File SHA-256:
  `42DC722E0028354BE71D025FE85AC551247649F3C0C0637C78019AEC21648D4C`.
- The canonical `DatabaseProfileControlPlaneService.BuildPostgreSqlOverrideProfile`
  uses `new Guid(MD5(UTF8(identity)))`, with identity
  `postgres-override:127.0.0.1:60920:cditall_primary_67c47aa1d495479495c55deaa4fdeedf:postgres`.
  Independent read-only calculation equals **exactly that profile ID**. The directory's
  organization workspace also uses `fe1dc18681827f77e9469c63114a51ff`. No password is
  included in this identity. This proves the database/root relationship independently
  of creation time or the generic `primary` name.

## Exact fixture and interrupted test

`tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs:1170`
defines
`CanDoItAll.Tests.Integration.AgentFramework.AgentFrameworkExecutionRunTrackingIntegrationTests.ContinueExecutionRunAsync_preserves_governance_failure_reason_after_pending_approval`.
It creates this unique environment prefix and a `primary` PostgreSQL lease, builds
the real test provider, replaces the runtime and invokes `UseDirectWorkspaceService`
before resolving `IAgentFrameworkWorkspaceService`. That is the fixture composition
whose new receipt dependency caused the independently diagnosed DI stall.
The frozen discovery log contains this method at line 2654; no completed result for
it appears in the retained Integration result identities.

`CanDoItAllTestEnvironment.Create` delegates to `TestFileSystem.CreateTemporaryRoot`
for `<temp>/<prefix>/<random-guid>`. `CreatePostgreSqlProfile` records the lease in
that environment. Normal disposal first drops its leased databases, then removes its
exact root. Killing the waiting host prevents those `await using` disposals. This
source lifecycle plus the deterministic profile binding identifies the residue;
timestamps alone were not used to infer ownership.

## Observed state and proposed parent cleanup

The exact root contains 11 files / 16 directories / 1515 file bytes, including empty
coordination locks, the activation record and test key/vault files. Secret values were
not read or printed. The root, every ancestor through the volume root, and enumerated
descendants have no reparse point. Original PIDs 49540, 49080, 36352 and 3192 were all
absent at the audit. These are point-in-time observations and must be refreshed before
destruction; no unrelated process or directory is covered by them.

Parent cleanup should first revalidate the exact container ID/label/mount and the
database name, absent owned process identities and zero clients. Preserve the fixture
container/volume and `postgres` database for remaining validation. Drop only the exact
leased database above using the existing fixture administration connection, then
verify that database is absent. Revalidate the recorded directory's fully resolved
containment beneath its exact prefix, unchanged activation binding and no reparse
escape, then remove only that GUID root with native PowerShell `-LiteralPath` and
verify absence. Record both database and directory outcomes separately. Keep the
retained run/stack/activation identity evidence needed for the audit.

Other older temporary roots observed under generic test prefixes have no binding to
this database and are **not approved cleanup targets**. This is exact residue
reconciliation, not an assertion that the entire system temporary directory is clean.
