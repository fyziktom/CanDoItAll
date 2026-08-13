# Validation strategy

## Principle

Use one build per checkpoint and many `--no-build --no-restore` focused test
runs. Do not repeatedly execute the broad stable suite.

## F00 characterization

- inspect exact branch and target commits;
- confirm clean package mode;
- run `git diff --check`;
- run the runtime runner self-test;
- create failing-first tests for F-001 through F-003.

No full build is required before the failing-first fixtures exist.

## F01–F03 implementation loop

For each subbundle:

1. build only the affected production project;
2. build the affected test project;
3. run only the named test class or exact fully qualified tests;
4. retain one TRX and a short source-diff record.

## Shared checkpoint C1 after F03

- restore if dependency files changed;
- clean Release package-mode solution build once;
- run:
  - `ProcessPlanMigrationIntegrationTests`;
  - plan persistence/hash unit tests affected by F01;
  - `LocalWorkspaceProcessHostTests`;
  - `ManagerProcessOwnershipTests`;
- run runtime portability Unit and Integration catalogs with the durable build
  stamp.

Do not run the browser catalog unless F01–F03 modify UI or browser code.

## F04 container checkpoint

- validate Docker policy;
- build the app image;
- prove `setsid` exists in the runtime image;
- start disposable app + database;
- wait for health/readiness;
- perform one simple app request;
- tear down containers, volumes, image and secret.

## F05 MAF checkpoint

Against the same build where possible, run the named classes:

- `MafPackageBaselineReflectionTests`;
- `MafApprovalSessionRoundTripTests`;
- `MafRuntimeArchitectureServicesTests`;
- `CanonicalAgentExecutionAuthorityResolverTests`;
- `AgentExecutionActivityCoordinatorTests`;
- `AgentFrameworkExecutionRunTrackingIntegrationTests`.

Add tests only for a demonstrated MAF 1.17 regression. Do not turn this into a
general MAF refactor.

## Final checkpoint C2

Required:

- exact candidate commit and clean status;
- package-mode Release build;
- C1 test set;
- F05 test set;
- runtime portability Unit + Integration;
- Docker smoke;
- migration upgrade/restart/idempotency proof;
- portability/static scan;
- secret scan;
- `git diff --check`.

Optional:

- the single runtime browser case, when Chromium is already available;
- broad stable suite only if the invalidation rules below trigger it.

## Broad-suite invalidation triggers

Run the broad stable suite once only when at least one is true:

- a public contract outside the named persistence/process/Manager boundary was
  changed;
- central DI composition changed beyond registrations required by the fixes;
- package versions changed again;
- database model changes extend beyond the correction migration;
- runtime catalog selection or shared test infrastructure changed;
- focused failures suggest an unrelated subsystem regression.

A documentation, evidence, checksum, or migration-fixture-only edit does not
trigger the broad suite.
