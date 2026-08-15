# Focused Unit, model, and compile proof

All commands ran from `C:\repositories\CanDoItAll`.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --no-restore --filter "FullyQualifiedName~LlmChatOperation"` | 0 | 15 passed, 0 failed, 0 skipped |
| `dotnet build tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -nologo -v:minimal` | 0 | 0 warnings, 0 errors |
| `dotnet build src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj --no-restore --no-dependencies -nologo -v:minimal` | 0 | 0 warnings, 0 errors after final logging correction |
| `dotnet ef migrations has-pending-model-changes --no-build --project src/Foundation/CanDoItAll.Migrations.PostgreSql --startup-project src/App/CanDoItAll.Web` | 0 | No changes have been made to the model since the last migration |
| `git diff --check` | 0 | pass |

The Unit slice directly covers unavailable dispatch, one live owner, fake-time pre-dispatch reclaim,
post-dispatch recovery, remote live-lease treatment without a local CTS, cancellation, idempotency, and
recovery transitions.

The first final build was sandbox-blocked from writing configured sibling Components outputs. The
unchanged approved rerun passed. Additional diagnostic/source-refresh builds exceeded the normal build
budget; no unfiltered test command was substituted. EF CLI 10.0.3 emitted an advisory mismatch against
runtime 10.0.4, but the pending-model check passed.
