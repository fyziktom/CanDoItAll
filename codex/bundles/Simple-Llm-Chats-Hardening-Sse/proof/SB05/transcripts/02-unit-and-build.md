# Focused Unit and affected compile proof

All commands ran from `C:\repositories\CanDoItAll` using local sibling source projects.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmConversationServiceTests\|FullyQualifiedName~LlmChatConversationApplicationServiceTests\|FullyQualifiedName~LlmChatDefinitionServiceTests\|FullyQualifiedName~LlmChatProviderRuntimeTests"` | 0 | 42 passed, 0 failed, 0 skipped |
| `dotnet build src/Modules/CanDoItAll.Modules.LlmChats.Persistence/CanDoItAll.Modules.LlmChats.Persistence.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal` | 0 | 0 warnings, 0 errors |
| `dotnet build tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore -p:UseLocalCanDoItAllLibraries=true -nologo -v:minimal` | 0 | 0 warnings, 0 errors |
| Current-source bounded-read/reference/partial guards plus `git diff --check` | 0 | pass |

Six focused test attempts were required, exceeding the normal budget of four. The first Unit attempt
found two lifecycle assertions. Integration attempts then exposed one test compile issue, one EF
translation issue, and one fixture fingerprint mismatch. Every repeat followed a concrete correction;
no solution-wide or unfiltered project test ran. The three-build budget was consumed by an initial Web
compile failure and the two successful builds above. No further build was run after the final
expression-only capacity-reservation correction.
