# Focused unit, build, and model evidence

All final commands ran from `C:\repositories\CanDoItAll` in local sibling-source mode.

| Command/slice | Exit | Result |
|---|---:|---|
| Focused operation/idempotency/cancellation/recovery/reducer/audit, conversation archive, and deadline unit filter | 0 | 19 passed, 0 failed, 0 skipped after the architecture split |
| `dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore -nologo -v:minimal --filter "FullyQualifiedName~LlmChatOperationTransitionRegressionTests"` | 0 | 1 passed, 0 failed, 0 skipped |
| `dotnet build src\Modules\CanDoItAll.Modules.LlmChats.Persistence\CanDoItAll.Modules.LlmChats.Persistence.csproj --no-restore -nologo -v:minimal` | 0 | 0 warnings, 0 errors |
| `dotnet build tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore -nologo -v:minimal` | 0 | 0 warnings, 0 errors after one stale test-double constructor was corrected |
| `dotnet ef migrations has-pending-model-changes --project src\Foundation\CanDoItAll.Migrations.PostgreSql --startup-project src\App\CanDoItAll.Web --no-build` | 0 | No pending model changes |

## Focused semantics

- same identity/fingerprint replay, including after archive;
- conflicting fingerprint before dispatch;
- one dispatch claim and no redispatch after possible dispatch;
- durable cancellation and finalization ordering;
- recovery-required escalation;
- direct/restart reducer parity;
- archive exclusion;
- deterministic deadline audit.

One initial focused test command did not compile because the newly added test lacked a namespace import.
The import was corrected before any behavioral result. This diagnostic rerun and the architecture-driven
refactor verification are recorded as governed deviations from the nominal command count.
