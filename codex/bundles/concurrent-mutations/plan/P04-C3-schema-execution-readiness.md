# C3 canonical schema and execution readiness

Prepared 2026-10-17 from read-only source inspection. This is an execution handoff, not a source release or validation result. Only this new note was written. No build, EF generation, discovery, test, scanner, host, CLI, database operation or C3 source edit was performed. Parent execution follows historical integration/proof and the independent performance checkpoint; main remains frozen until the parent releases it.

## Frozen basis and transfer order

- C3 source inventory: `validation/P04-C3-source-inventory.json`, SHA256 `8B3FF71B2F5558784E64F3CA3FC12F58B94074D26833CA6AFC95BB1B0E69E232`.
- Managed ignored preflight: `.artifacts/concurrent-mutations/candidates/c3-integration-preflight/preflight.json`, SHA256 `CEEE892AEE5B24071EE896A818A8CC936851F053F75532E76841EDC7207B2FD1`; adjacent `integration-plan.md`, SHA256 `F57C22B36E35A932CAA49B9998BDA17D376F60CD042D14E2273C79DFB948827A`.
- This read rechecked all 25 inventory paths: all main paths still match their recorded parent predecessor or expected absence; 24 managed paths match C3. The only managed successor is the already reviewed combined Workbench registration file.
- The canonical snapshot currently contains 168 distinct entity names. Workbench's explicit model has 16 configurations including the new receipt; its predecessor has 15. Resources still registers only `ProjectResourceConfiguration` / `Resources_ProjectResources`. These are source observations, not a compiled-model result.

Integrate and validate the frozen historical graph first. Then transfer the unchanged C3 inputs with an explicit merge of `WorkbenchModuleServiceCollectionExtensions.cs`; never replace that file with the old C3-only image. Its main predecessor is `5732A4A6518FEB83FA58F483F71F5086531A6C4A234856A9532600B26C0D3443`, C3-only image is `54DB12820CB973123F26CC66A13049C6DE9CC59E8190E000B6C1B42E9E355F4E`, and current combined image is `51C85F83CD3BA029B598D73612D33DBD81644B366E13D546BFB0CB014B63A60C`. Preserve both C3 options/retention and the historical `IAgentHistoricalReceiptSourceAuthorityProvider` registration for `ProjectStructureExecutionAuthorityProvider`. Recheck the actual parent predecessor after historical integration and record the resulting merge hash.

`RuntimeHostServiceCollectionExtensions.cs` has separate historical successors and needs no C3 change: it already calls `AddFiniteOperationPools`, registers one `FiniteOperationMaintenance` worker and calls `AddWorkbenchModule(configuration)`. Do not overwrite it from a stale C3/preflight copy. Do not copy unrelated bundle review documents, frozen historical files, or parked performance changes while integrating C3.

Resource readiness remains a separate design. Keep its future model/receipt/epoch registrations out of the generation input. Do not exclude its existing assembly to hide a model difference: `ModuleAssemblies.All` is the complete canonical catalog. If another approved model change lands before this lane, stop and rebase the explicit type-set comparison; do not change a numeric assertion until the additional entity is separately explained. This C3 delta is exactly one entity, not a general permission to accept a new total.

## Exact remaining source amendments

These edits are not made by this note. The first two and migration/test additions belong to the parent integration checkpoint.

| Owner/input | Required amendment and invariant |
| --- | --- |
| `src/App/CanDoItAll.Composition/FiniteOperationComposition.cs` | Add the Workbench namespace and one singleton `WorkbenchTaskPlanFiniteOperations.Create(provider.GetRequiredService<IOptions<ProjectTaskPlanOperationOptions>>().Value)` alongside the Projects registration. Current predecessor SHA256 `6BD3AB6F90AEE98217EEB1186283BE6879634ADAFC64D117EC580EF4A7CC6D8F`. Reuse the existing method; no second registry/worker. |
| `src/App/CanDoItAll.Web/Infrastructure/DatasetActivationCommand.cs` | Bind `ProjectTaskPlanOperationOptions.SectionName`, apply the same `IsValid` validation, and explicitly resolve its `IOptions<...>.Value` before profile resolution/EnsureDatabase/Migrate, beside the existing Projects validation. Current predecessor `3CDB8D39D117D32A3C78AD957662F462FE5F54A01F2B8C2C48CEF370BFBC99AB`. The command builds but never starts its WebApplication. Do not call `AddWorkbenchModule` or runtime/hosted-service composition merely to get these options. |
| Canonical migration project | Generate one new `AddWorkbenchTaskPlanReceipts` migration/designer and updated `AppDbContextModelSnapshot.cs`; review and add the explicit active-pool installation/preflight/Down refusal described below. No hand-maintained second model. |
| New Integration `ProjectTaskPlanMigrationTests.cs` | Author the three proposed migration Facts below. They do not exist yet and are not counted as authored. |
| Existing Integration `DatasetActivationCommandProcessTests.cs` | Preserve its two actual-process Facts, but add `PoolId` to both SQL projection records and ordering. Assert exact Projects+Workbench sets per namespace, epoch-one readiness, unchanged issued times on resume, closure of both old pools and opening of both new pools on reactivation. Current single-global-row assertions and projections cannot prove the two-pool contract. Extend a meaningful existing arrangement to reject invalid Workbench options before database work, without starting writers. |
| Existing Integration `FiniteOperationMigrationTests.cs` | Preserve four Facts and their C2 intent. Latest-upgrade comparison must retain the exact original Projects state/epoch/receipt/lifecycle tuples while separately verifying the legitimate C3 active-pool addition. Its current unfiltered `ReadRetainedAsync` and `Assert.All(... PoolId == Projects)` are now obsolete. The C2 Down-refusal arrangement must start at the exact C2 migration, otherwise C3's earlier Down refusal would mask the behavior it claims to test. Keep C3 latest-schema/Down proof in the new migration lane. Do not broadly accept any pool or exception. |

The frozen five model/transfer seams remain required: `ProjectTransferTargetStateArchitectureTests`, `WorkbenchOwnerPersistenceTests`, `ProjectTransferOwnerInspectionTests`, `SharedProviderPremergeUpgradeTests`, and `HistoricalProjectMigrationAssertions`. The helper has no rows; its three real caller classes are listed below. Preserve their historical upgrade/downgrade boundaries and byte comparisons. Their ordinary downgrade arrangements already stop at their named historical migration before going down, rather than attempting to bypass the irreversible head.

There is one compiled pool list, used by two entry compositions, not two independent lists to maintain. Expected normal/offline set:

| Pool | Family | Protocol / payload |
| --- | --- | --- |
| Projects `e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376` | Definition `574607ce-cb66-4783-9675-8a59d4383ab4` | 1 / existing 2 MiB |
| Workbench `c91aaef3-cfc1-4c29-88aa-10693fecd4e3` | Task plan `d8835be0-6fd5-4d9d-8162-1733367fc22e` | 1 / 2 MiB |

`Workbench:TaskPlanOperations` defaults are AdmissionWindow one day, ReplayRetention seven days, MutationDeadline 30 seconds, StatementDeadline 10 seconds, LockDeadline five seconds, MaintenanceInterval 60 seconds, MaintenanceWarningAfter five minutes. These describe future issuance/current scheduling; existing promises are never recomputed from new settings. Normal module registration already binds/validates these options and registers exactly one singleton `ProjectTaskPlanReceiptRetention`. Its immutable options/coordinator create an enlisted context per call; it must not resolve Agent workspaces/providers. `FiniteOperationReceiptMaintenance` requires exactly one participant for every registered family and rejects extra participants. In particular, transferring the participant without the compiled pool is not a runnable intermediate application checkpoint.

Publisher, publication authority, receipt-access adapter, tool, endpoint and producers stay internal/unregistered. Passing dormant C3 does not activate ordinary Agent policy, R1d saved dispatch, Process/Workflow planning or C4.

## Canonical generation and SQL review

The existing design-time owner is `PostgreSqlAppDbContextFactory`: it calls `AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All)`, then creates canonical `AppDbContext`. `AppDbContext.OnModelCreating` applies Infrastructure and every registered owner configuration. Neither these files nor `ModuleAssemblies` require C3 changes. Use the repository SDK (`global.json`: 10.0.302, latest patch) and existing EF tooling; do not install/upgrade tools as an incidental step.

The predecessor is exactly `20261016233100_ExtractFiniteOperationEpochMetadata`. Preserve its source (`570D349293527963DCE8F6E71B47EEE61D04BA57501EBF7B3FF3EFBBB35A7A9F`) and designer (`1FBB4FB747F30D2C808FF54FAAA3793F4F1C72320C75AA40703A1DCA3DCC3CA9`). Before generation the current snapshot is `B78FAEF2D168FCBC6D2E319697177CEB98D2F48004D524D4442031316E5A7E58`. This pending one-entity difference is expected before generation; do not demand a pre-generation no-pending-model pass or erase it by changing the owner model.

After source release, from the parent-approved checkout root, serialize the following standard Release builds. Project references build their dependencies; these explicit owners make the recorded checkpoint clear. No custom `OutputPath`, `BaseIntermediateOutputPath` or configuration is permitted.

```powershell
$workbenchProject = 'src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj'
$compositionProject = 'src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj'
$migrationProject = 'src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj'
$webProject = 'src/App/CanDoItAll.Web/CanDoItAll.Web.csproj'
dotnet build $workbenchProject --configuration Release /m:1
dotnet build $compositionProject --configuration Release /m:1
dotnet build $migrationProject --configuration Release /m:1
dotnet build $webProject --configuration Release /m:1
dotnet ef migrations add AddWorkbenchTaskPlanReceipts --project $migrationProject --startup-project $webProject --context AppDbContext --configuration Release --no-build --output-dir Migrations
```

Use only the already owned fixture profile if an explicit design-time connection environment value is supplied (`CANDOITALL_MIGRATIONS_POSTGRES_CONNECTION`); never put credentials in command arguments/logs. Generation is a model operation, not authorization to update any database. Do not invoke `database update` or the actual offline command during generation. Do not load a partial Workbench-only model to obtain a cleaner diff.

Inspect all three generated files. The canonical type-set delta must be only `CanDoItAll.Modules.Workbench.ProjectTaskPlanReceiptRecord` / `Workbench_TaskPlanReceipts`, 168→169 for this exact predecessor, Workbench15→16. Expected receipt address PK is `(NamespaceId, Epoch, CallerKind, CallerKey, OperationId)`, seven reviewed check constraints, required payload/key/digest fields, fixed 32-byte keys/digests, payload1..2MiB. No foreign key, cascade, trigger, generated/default authority, speculative index, Resource entity change, Projects receipt rewrite, neutral-table DDL change or unrelated drop/recreate belongs in this migration. Compare mapping/type sets, not only counts.

Generated DDL does not express the whole installation protocol. The same stopped-host migration transaction must:

1. Validate the exact supported predecessor schema/metadata and acquire bounded lifecycle/neutral metadata exclusion in the existing lifecycle-first order. Unknown pools, partial metadata, non-Active lifecycle, unsupported protocol or unexpected existing Workbench metadata refuse explicitly; no `ON CONFLICT` adoption or healing.
2. Create only the receipt table. For an existing valid Active namespace, insert Workbench state `(CurrentEpoch=1, RetiredThroughEpoch=0, RetentionProtocolVersion=1)` and Open epoch1 using one captured PostgreSQL time; persist the reviewed initial one-day admission, 30,000 ms mutation and 604,800-second replay promise. Runtime configuration must not retroactively alter this issued migration promise.
3. Leave a genuinely absent lifecycle uninitialized. Bootstrap later creates the whole compiled set. Do not backfill Workbench into old inactive namespaces or touch Projects state/epoch/receipt bytes. Ready/Attach/Resume remain validation/recovery, never missing-state repair.
4. Refuse Down permanently with nontransient P0001 before any schema/history/data mutation. The new binary and offline CLI must advertise the same set; an older Projects-only binary must refuse the upgraded active binding.

Review exact SQL catalog preflight and transactional rollback with the migration author; this note does not supply unreviewed SQL. Then rebuild migration and Web after editing the generated migration. Record the actual generated migration ID as `$c3MigrationId`; do not invent its timestamp. After these rebuilds:

```powershell
dotnet build $migrationProject --configuration Release /m:1
dotnet build $webProject --configuration Release /m:1
dotnet ef migrations has-pending-model-changes --project $migrationProject --startup-project $webProject --context AppDbContext --configuration Release --no-build
dotnet ef migrations script 20261016233100_ExtractFiniteOperationEpochMetadata $c3MigrationId --idempotent --project $migrationProject --startup-project $webProject --context AppDbContext --configuration Release --no-build --output .artifacts/concurrent-mutations/validation/c3-schema-execution/c3-upgrade.sql
```

The parent records ownership of that ignored proof directory before creating it. Review the script without applying it, including no schema suppression outside the transaction, no authority defaults and preservation of every prior migration. Actual migration/activation proof uses only the reused isolated PostgreSQL18 fixture and its exact owned leases, never the ordinary 5032 profile.

The three prospective `ProjectTaskPlanMigrationTests` Facts must establish: (1) fresh and Active upgrade, exact Projects bytes/promises, repeated restart and Prepared-plan recovery; (2) predecessor drift/unknown/partial/new-pool/older-binary refusal with complete rollback and no history adoption; (3) canonical/owner schema parity, exact keys/checks/no cross-owner effects and irreversible Down. Author and independently recount these before adding `3` to any executable manifest. Migration metadata/parity alone cannot replace their runtime arrangements.

## Source-derived owning lanes

All counts below were read from current managed attributes and their containing classes, including both `DatasetActivationCoordinatorTests` partials. There are no dynamic data providers, skips or inherited test bases in these selections. Inline rows use serializable booleans/enums/strings, so discovery and execution expectations agree. B1 `ProjectTaskPlanBasisTests` is 12 Facts; occurrences of the helper expression `[Fact()]` inside collection expressions are not xUnit attributes.

For every ordinary row below the exact filter is `FullyQualifiedName~<fully-qualified-class>.&Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true`. The trailing dot bounds the class selector. `HostPlatform` is not excluded. The complete names are deliberate: two Unit classes have additional namespace segments.

| Owning assembly | Fully qualified class | Facts + inline rows | Expected discovery / execution |
| --- | --- | ---: | ---: |
| Unit | `CanDoItAll.Tests.Unit.ProjectTaskPlanOperationContractTests` | 10 + 0 | 10 / 10 |
| Unit | `CanDoItAll.Tests.Unit.ProjectTaskPlanBasisTests` | 12 + 0 | 12 / 12 |
| Unit | `CanDoItAll.Tests.Unit.ProjectTaskPlanTaskStagingTests` | 10 + 0 | 10 / 10 |
| Unit | `CanDoItAll.Tests.Unit.Projects.ProjectTransferTargetStateArchitectureTests` | 3 + 0 | 3 / 3 |
| Unit | `CanDoItAll.Tests.Unit.FiniteOperationContractTests` | 8 + 0 | 8 / 8 |
| Unit | `CanDoItAll.Tests.Unit.DatasetActivationCommandTests` | 1 + 12 | 13 / 13 |
| Unit | `CanDoItAll.Tests.Unit.Infrastructure.DatabaseCanonicalityArchitectureTests` | 4 + 0 | 4 / 4 |
| Integration | `CanDoItAll.Tests.Integration.ProjectTaskPlanOperationTests` | 14 + 0 | 14 / 14 |
| Integration | `CanDoItAll.Tests.Integration.ProjectTaskPlanCurrentnessTests` | 12 + 0 | 12 / 12 |
| Integration | `CanDoItAll.Tests.Integration.ProjectTaskPlanRetentionTests` | 6 + 0 | 6 / 6 |
| Integration | `CanDoItAll.Tests.Integration.ProjectTaskPlanBasisCaptureTests` | 10 + 0 | 10 / 10 |
| Integration | `CanDoItAll.Tests.Integration.ProjectTaskPlanStagingPersistenceTests` | 6 + 0 | 6 / 6 |
| Integration | `CanDoItAll.Tests.Integration.WorkbenchOwnerPersistenceTests` | 5 + 2 | 7 / 7 |
| Integration | `CanDoItAll.Tests.Integration.ProjectTransferOwnerInspectionTests` | 5 + 12 | 17 / 17 |
| Integration | `CanDoItAll.Tests.Integration.SharedProviderPremergeUpgradeTests` | 1 + 0 | 1 / 1 |
| Integration | `CanDoItAll.Tests.Integration.SourceBoundExecutionAdmissionMigrationTests` | 4 + 6 | 10 / 10 |
| Integration | `CanDoItAll.Tests.Integration.WorkAssignmentMigrationIntegrationTests` | 5 + 5 | 10 / 10 |
| Integration | `CanDoItAll.Tests.Integration.WorkflowReceiptMigrationIntegrationTests` | 2 + 4 | 6 / 6 |
| Integration | `CanDoItAll.Tests.Integration.FiniteOperationMigrationTests` | 4 + 0 | 4 / 4 |
| Integration | `CanDoItAll.Tests.Integration.FiniteOperationPersistenceTests` | 12 + 0 | 12 / 12 |
| Integration | `CanDoItAll.Tests.Integration.DatasetActivationCoordinatorTests` | 9 + 0 | 9 / 9 |

Authored ordinary subtotal: **60 Unit +124 Integration =184**. Within it C3 contributes **10+32=42 new**, B1/C1 contribute38, and the remaining104 are focused regressions. The 21 class selectors have no overlap. The three proposed migration Facts would make187 only after source exists and is recounted. This is a selected affected-scope inventory, not a Broad Stable total or a claim to close every C4 writer/performance perimeter.

Separate explicit Integration selections:

- Two `LiveProcess`/`HostPlatform` rows: `FullyQualifiedName~CanDoItAll.Tests.Integration.DatasetActivationCommandProcessTests.&Category=LiveProcess&Category!=Quarantined&RequiresHostDocker!=true`. Recount after the required two-pool fixture adaptation; preserving two Facts preserves expected2. These launch the standard Release Web DLL via `IntegrationTestPaths.BuildConfiguration`, maintain exact owned profile/child records and deliberately occupy the requested loopback port to prove no web host starts.
- One opt-in performance row: `FullyQualifiedName=CanDoItAll.Tests.Integration.ProjectTaskPlanOperationPerformanceTests.Measure_bounded_dormant_task_plan_publication_and_exact_replay&Category=LongRunning&Category!=Playwright&Category!=LiveProcess&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true`. This is characterization, not a capacity acceptance test. Internal phases, escaped-text maximums, sustained arrivals and multi-family fairness remain unqualified.

The direct C2 coordinator/epoch fixtures intentionally construct exact old/test pool sets against fresh uninitialized metadata; do not replace their narrow fixture registrations with the application compiled list. Conversely, `ProjectTaskPlanOperationFixture.CreateAsync` uses real `TestApplication` migration/bootstrap and registered pools, and only removes its owned periodic maintenance worker. It must not be made runnable early by injecting a fake Workbench pool or bypassing migration. The production composition proof must assert exact two-pool/two-participant identity, not merely that Workbench appears somewhere.

## Build, freeze, discovery and execution sequence

After all production/migration and necessary test source amendments, build the two owning test projects, serialized, with current standard outputs. Integration references real Web, Support and its build-only helper hosts; retain those references and historical helper README. No new test assembly is needed. Components/browser are not C3 owners; their historical proof remains a prerequisite, not a substitute for the selected rows.

```powershell
$unitProject = 'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj'
$integrationProject = 'tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj'
dotnet build $unitProject --configuration Release /m:1
dotnet build $integrationProject --configuration Release /m:1
```

Create an exact source/binary manifest and source-derived lane counts only after the final source is fixed. Follow `docs/testing.md` and `.github/workflows/ci.yml`: run Python portability-baseline/secrets tool tests, rendering tool tests/register check, full portability scan **without `--tracked-only` while new protected files are untracked**, review every added/stale finding, repair genuine issues and regenerate, then only parent-reviewed baseline refresh and final no-write enforcement. Run documentation validation for generated/new maintained documentation. None of this note's source reading is that required gate.

For each ordinary selector, let `$testProject` be its actual Unit/Integration project, `$testFilter` its exact table-derived expression and `$laneId` its unique retained evidence name. Discovery is build-backed; it must not use `--no-build` to justify stale dependencies. Execute only after the expected count matches and record full source/binary identity. The parent may union selectors only after preserving parentheses and independently summing the disjoint counts.

```powershell
dotnet test $testProject --configuration Release --list-tests --filter $testFilter /m:1
dotnet test $testProject --configuration Release --no-build --no-restore --filter $testFilter --logger "trx;LogFileName=$laneId.trx" --results-directory .artifacts/concurrent-mutations/validation/c3-schema-execution /m:1
```

Discovery/execute the migration three first once authored, then C3 contract/operation/currentness/retention, B1/C1 and remaining affected regressions; keep every failure/mismatch/skip explicit. Run the two actual offline processes separately after Web output release and ownership checks. Only then schedule the one isolated performance characterization with no parallel builds/other load. These commands are future parent instructions, not operations performed here. A source edit after discovery invalidates that frozen input; rebuild and repeat affected discovery before no-build execution.

Finish by verifying owned process exit and exact lease/root cleanup, preserving failed/incomplete proof and reporting any remaining resource. No global Docker cleanup, ordinary-profile writes, custom build output or silent fixture replacement is authorized. Keep existing issued receipt/history byte evidence. Dormant C3 cannot be marked end-to-end ordinary Agent planning or persisted policy activation: R1d, C4 real decision authority, remaining actual writer overlaps, work-time semantics, restart mutation admission and measured multi-owner capacity are separate gates.
