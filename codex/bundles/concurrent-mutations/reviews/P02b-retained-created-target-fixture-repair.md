# Retained created-target lifetime fixture repair

Status: source-only test repair; no production change or validation run.

The retained Stable failure in
`ProcessCatalogAuthorityPersistenceTests.ProjectWriters.Retained_created_target_cannot_be_rebound_to_a_recreated_ID_or_an_unrelated_existing_project`
combined a newly read recreated project expectation with source authorization retaining
the original lifetime. `ProjectSourceMutationScope.BeginCoreAsync` now correctly rejects
that contradictory pair as `ArgumentException` with parameter `expectedProjectAdmissions`
before acquiring source authority. That is distinct from a stale but internally
consistent original editor/reservation being refused by the owner.

The fixture now captures the editor before delete/recreate. Its existing exact
`InvalidOperationException` assertion therefore still reaches
`ProjectWriteAdmissionService.RequireConsumedCreationsForMutationAsync`, proving that
the retained consumed reservation cannot authorize a different live lifetime. A second
assertion deliberately pairs the fresh recreated editor with the old authorization and
requires the exact argument exception/parameter. The lifetime difference, unchanged
recreated content and unrelated-target authorization rejection remain explicit.

One existing Fact changes; no added discovery rows. Focused filter:

```text
FullyQualifiedName=CanDoItAll.Tests.Integration.Processes.ProcessCatalogAuthorityPersistenceTests.Retained_created_target_cannot_be_rebound_to_a_recreated_ID_or_an_unrelated_existing_project
```

Expected discovery/execution: **1 row** after the parent refreshes the standard
Integration assembly. Build, focused execution and mandatory portability-static remain
parent-owned pending gates. Main checkout and historical failure evidence are unchanged.
