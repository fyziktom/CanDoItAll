# Atomic project search: delayed acknowledgement fixture repair

Status: source-only test repair; no production change, build or executed proof.

The former two-row `Delayed_postcommit_search...` fixture waited for a second Projects
context. That context belonged to the old postcommit search path and no longer occurs:
`ProjectDefinitionWriter.ExecuteCoreAsync` now stages search through
`UpsertForMutationAsync` inside the exact owner transaction before commit. Waiting for
a removed implementation detail timed out without testing the intended race.

The renamed two-row
`Delayed_commit_acknowledgement_cannot_resurrect_a_deleted_project_or_overwrite_its_new_lifetime`
uses the delayed writer's existing configured Projects options plus a test-only
`DbTransactionInterceptor.TransactionCommittedAsync`. That callback starts after the
actual PostgreSQL commit, withholds its acknowledgement and releases in `finally`.
The ordinary independent scope verifies both project and search are already visible,
deletes the project and optionally recreates its public ID with a distinct lifetime.
After releasing the original acknowledgement, the original save must still report
success, and search must remain empty or contain only the recreated title.

The interceptor belongs only to the delayed writer's factory; deletion/recreation
uses unmodified DI services and cannot be paused by it. The source asserts exactly
one delayed owner commit and that the original save is incomplete at the barrier.
It adds no production hook, retry, timeout increase or synthetic postcommit search.
The existing 30-second cancellation bound and deterministic release remain.

One Theory retains exactly two InlineData rows (`false`, `true`); class source count
is unchanged at five rows (three Facts plus this two-row Theory). Focused filter:

```text
FullyQualifiedName=CanDoItAll.Tests.Integration.ProjectWriteAdmissionIntegrationTests.Delayed_commit_acknowledgement_cannot_resurrect_a_deleted_project_or_overwrite_its_new_lifetime
```

Expected focused discovery/execution: **2** after the parent refreshes Integration
outputs. Parent-owned build, focused execution and portability-static remain pending.
The frozen main source and historical failed results are unchanged.
