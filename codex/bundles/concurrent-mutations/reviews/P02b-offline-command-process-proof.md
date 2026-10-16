# Offline activation executable proof

Status: two new LiveProcess/HostPlatform facts prepared in `DatasetActivationCommandProcessTests`; no execution claimed. Parent runs build-backed discovery and the explicit filter `FullyQualifiedName~DatasetActivationCommandProcessTests` after the standard Web and Integration outputs are current.

The tests resolve the real `CanDoItAll.Web.dll` through `IntegrationTestPaths.BuildConfiguration`. Each child runs its actual `dataset-activation` entry point; no in-process parser/service substitute stands in for executable wiring. The fixture owns a new PostgreSQL database because bootstrap needs a genuinely absent activation binding, and owns its control-plane, workspace, state, logs and temporary roots through `CanDoItAllTestEnvironment`. It configures ordinary test services without schema bootstrap, resolves the canonical explicit override profile, and passes the exact configuration to the child through environment variables. No database connection or credential is placed in arguments, resource records or assertion output.

The first case launches separate bootstrap, resume, reactivate, repeated resume and stale-expected-namespace refusal processes. It reads actual activation files and SQL lifecycle/epoch rows. Resume preserves namespace, maintenance identity and epoch deadlines. Reactivation creates a fresh namespace, closes the original epoch without changing its original admission deadline, and opens the new namespace's first epoch. A stale reactivation leaves both the activation file and SQL rows unchanged.

The second case migrates only the owned database, installs a fixture trigger that refuses epoch initialization, then launches the actual bootstrap executable. It requires process failure and an exact retained Prepared activation plan with no committed owner epochs. After removing that fixture trigger, a separate resume process must commit the same prepared namespace/maintenance/plan. Repeated resume must preserve the SQL state.

Every launch writes an ownership record before `Process.Start`, updates it with the owned child PID, and records verified exit. A loopback listener is held on the URL supplied to the child: successful CLI completion while that endpoint remains occupied demonstrates that the offline command did not bind/start the normal Web host. No hosted service is started by the parent fixture. The child has a 90-second exit deadline and finally kills only its own process tree if needed, then waits for exit before fixture database/root cleanup. Output is drained with a bounded tail and never emitted as raw test diagnostics. The ordinary application/profile/port 5032 are not referenced or modified.

The reviewed first-run path is intentional: the canonical runtime profile accessor resolves the explicit configured override. The parent and child receive the same owned database endpoint and workspace configuration, producing the same explicit profile ID; the command then checks PostgreSQL, migrates and activates that profile. The command does not silently choose an ordinary persisted active profile.

Static investigation of the failed early child starts found two missing runtime-only
boundaries in the common Infrastructure registration: StorageProfileTransferService
requires Workbench's IStorageTransferProvenancePolicy, and six Storage implementations
require Security's IStorageSecretResolver. Development's build-time DI validation
correctly refuses these unrelated descriptors before any Prepared activation record.
The offline command now explicitly omits exactly seven typed implementation registrations:
StorageProfileTransferService, StorageTransferPipeline, StorageConnectionTestService,
IpfsStorageDriver, FtpStorageDriver, IpfsStorageBrowseDriver and FtpStorageBrowseDriver.
This is local to offline composition; regular application registration and validation
remain unchanged. No fake authority/secret resolver, disabled validation or full module
startup is introduced. The command resolves only its concrete profile service,
configured database driver/factory, activation options and coordinator. Both existing
process facts retain their count and actual SQL/file assertions; this repair is unrun
until the parent records the new executable results.
