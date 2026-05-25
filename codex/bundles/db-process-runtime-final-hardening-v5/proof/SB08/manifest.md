# Proof manifest SB08

## Status

Completed with classified broad-suite caveats.

## Changed files

Primary process DB hardening files:

- `5D460D22065B2F3AE3763C66D30B0572FE5143A46B9E7E0D16740BB19BFFCA9F` `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs`
- `E0557314887CB2C243AF39EFD85485A23D05E67E62E83DE91D8F6BD29D206AA7` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs`
- `8205AFB6B9DD9671E6AE3D0AA0A5DF8215BA66DB534D66AA8B4D9AF372DCDC61` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `5DDA98D5FF8987DE8AE057E157B761C41C02891625E82DC88E9B2EBCB10A39CB` `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs`
- `590F0A5C9EB47723A0403A72A8E41593B6EB9F6A486B0942C42B8F32125179E3` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `A91578D23298CDD94DD99FA4B55E7D128B9FAC9FA2EAF35F2AC4DC3180CFE3FD` `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `DF85A1ADE2AF49EA6DD13FC44AA2544BC4185C625EE7B385BBBC248674F9B248` `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `C35401A000726176963B109DA8852815FD1342CF5F0B9CFC5E5820037EF7C84F` `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260524183000_ProcessClaimHotPathIndexes.cs`

Validation test files:

- `580997B600C2B4516D3B5ED17A4DB2531E37173108E8FD38C5CF433E4C86655B` `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlClaimQueryPlanIntegrationTests.cs`
- `99AFCE2A484F55907C605F18232CFCBAA93E5660DC7E793ABBC4744C1E4C18E9` `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlRuntimeThroughputBenchmarkTests.cs`
- `4B8850C1BFE7D68243D0D5758A6F1006898907927A9ACE700C4016A666861D54` `repo://tests/CanDoItAll.Tests.Integration/ProcessDatabaseRedTeamSourceInvariantTests.cs`

## Validation commands

- Restore: `dotnet restore CanDoItAll.slnx` -> `bundle://proof/SB08/full-restore.log` (`A61A9AA080A82C8EA753CE1B095753E72BAC8E99E3603AF3F647E21E6A6F37ED`)
- Build: `dotnet build CanDoItAll.slnx --no-restore -v:minimal` -> `bundle://proof/SB08/full-build.log` (`CD6D14A3136647516AAE60A51A42B348FF124E2151AA56481DD229E7A314078D`)
- Unit tests: `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build -v:minimal` -> `bundle://proof/SB08/full-unit-tests.log` (`3117D06D2A1DDC2F7FFEA6B826F67B05378BE2B1087386B95765CDEC9B8B82C6`)
- Full integration: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build -v:minimal` -> `bundle://proof/SB08/full-integration-tests.log` (`0916564EC8CA67C0C8FE05EF8167319DEEC6D3FBE413F047EAE565551837BA8C`)
- Failed integration rerun after local `postgres` role repair -> `bundle://proof/SB08/integration-failed-tests-after-postgres-role-fix.log` (`A7A25C4BEAB82AF362BE9FA93E6B814236E1F406C87CD971D02903BF40C9F23D`)
- Main component tests -> `bundle://proof/SB08/full-component-tests.log` (`F163A1A1E7E84046ACC6BD8B39F1EA7DD348D0A45E45EE39EF65BB98EBBAB98B`)
- Main component tests excluding `ProjectsPageTests` -> `bundle://proof/SB08/component-tests-without-projects-page.log` (`F6814E68A0622BE50CAF5CDADC4DF247841FBAA0820E59E509182CB3B1205A0B`)
- MCP component tests -> `bundle://proof/SB08/full-mcp-component-tests.log` (`6F3EB01DF6D530075EBD2BC7E33ACBE2E0B216A8763633A6E93E7AAFFE5EA364`)
- Focused process DB tests -> `bundle://proof/SB08/focused-process-db-tests.log` (`70A89669C0EE2BCBDDCFD06174D229823D53A46334857EE3C1ADEE1E6107AD3B`)
- EF drift: `dotnet ef migrations has-pending-model-changes ... --context AppDbContext` -> `bundle://proof/SB08/ef-pending-model-changes.log` (`233F2B7B10E08BCC823924B7E807EDAE994A085498E2DBE2E025FCDF6BDA9861`)
- Runtime residue audit: `scripts/audit_process_db_canonicality.ps1` -> `bundle://proof/SB08/runtime-residue-audit.log` (`3522C9CA8833ADC7661A9A193C828E8A2485BD772907F5529F03D17922D66FDF`)
- Final bundle closure validator: `python scripts/validate_bundle.py --stage completed` -> `bundle://proof/SB08/final-validate.log` (`E4F0A467AAF9A28C7821DB62335D83882A6BDE5727A20A75D1E9D8C8C6F0BA90`)

## Source assertions

- Startup recovery only releases expired automation dispatch leases; live leases are preserved.
- Process dispatch claims the step before hydrating detailed candidates, and stale workers must renew/check claim ownership before artifact projection or completion transition.
- Process outbox automation dispatch enqueue dedupes pending duplicate commands by canonical run/step/trigger identity.
- Outbox, automation delivery, and connector command finalization are conditional on live lease token ownership and emit stale-finalization metrics when ownership is lost.
- PostgreSQL hot claim paths have explicit partial indexes and `FOR UPDATE SKIP LOCKED` query-plan proof.

## Production behavior artifact matrix

| Behavior | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Expired startup dispatch lease recovery | `ProcessRunRecoveryWorker` | Process dispatch workers | Startup scan only releases expired claims | SB02 tests and SB08 focused process DB tests |
| Long-running dispatch heartbeat | `ProcessDispatchLeaseHeartbeat` | Dispatch finalization path | Renew outer outbox and step claim until work completes or claim is lost | SB03 tests and SB07 red-team tests |
| Stable outbox side-effect idempotency | `ProcessOutbox` | Activity/audit and automation dispatch consumers | Reuse stable idempotency keys and suppress duplicate pending dispatch commands | SB04 tests and SB06 duplicate-suppression metric |
| Runtime claim metrics | Outbox, automation, connector services | OpenTelemetry meter listeners and operators | Record claim, process, stale-finalization, duplicate-suppression, and batch-duration signals | SB06 benchmark and metrics listener proof |

## Semantic adequacy

SB08 confirms the hardening-specific semantics with focused tests, query plans, benchmarks, and source audits. The broader repository validation is not completely green, but the failures are classified and are outside this process DB hardening touch set.

## Residual risks

- Full integration has three classified runtime-switching failures in untouched tests after environment repair.
- Main bUnit component suite has classified project/project-structure failures and hang behavior in untouched component tests.
- Build still emits existing `MSB3277` EF Core assembly-version conflict warnings.
