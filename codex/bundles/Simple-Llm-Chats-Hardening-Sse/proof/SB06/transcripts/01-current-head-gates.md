# CP1 current-head behavioral gates

All final commands ran from `C:\repositories\CanDoItAll` using local sibling source projects.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal --filter "FullyQualifiedName~LlmChat\|FullyQualifiedName~LlmConversationServiceTests\|FullyQualifiedName~DatabaseRuntimeStateTests"` | 0 | 87 passed, 0 failed, 0 skipped |
| `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal --filter "FullyQualifiedName~LlmChat"` | 0 | 22 passed, 0 failed, 0 skipped |
| `dotnet build tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal` | 0 | 0 warnings, 0 errors |
| `dotnet build tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal` | 0 | 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes --no-build --project src/Foundation/CanDoItAll.Migrations.PostgreSql --startup-project src/App/CanDoItAll.Web` | 0 | No changes have been made to the model since the last migration |

The Integration union includes the SB01 canonical transaction/migration/transfer owners, SB02
turn/operation and real-host API owners, SB03 profile-switch case, SB04 lease/request-lifetime cases,
and SB05 2,000-message bounded-read case.

Command-attempt deviation: the Unit union first found a missing read-store registration in its minimal
composition fixture. The Integration sandbox run produced 12 passes and 10 identical LocalAppData lock
denials; its unchanged authorized rerun passed 22/22. After source cleanup, final no-build unions above
bound evidence to the actual head. One deliberate interface-removal build exposed three remaining direct
test callers; both final builds above are clean. No unfiltered project or solution test ran.
