# SB33 Validation

## Commands

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentProviderFailureDisplayFormatterTests|FullyQualifiedName~ProjectStructureRuntimeLauncherTests|FullyQualifiedName~ProcessTemplateRuntimeWritebackTextTests" -p:OutDir=C:\repositories\CanDoItAll\artifacts\test-out\unit-sb33-final\
```

Result: passed 18/18.

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~FileSandboxWorkspaceStoreLockIntegrationTests.ChatSessionStore_creates_and_updates_split_session_projection" -m:1 -p:OutDir=C:\repositories\CanDoItAll\artifacts\test-out\integration-sb33\
```

Result: passed 1/1.

```powershell
dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj -m:1 -p:OutDir=C:\repositories\CanDoItAll\artifacts\test-out\web-sb33-serial\
```

Result: build succeeded with 0 warnings and 0 errors.

```powershell
git diff --check
```

Result: no whitespace errors. The transcript contains only line-ending normalization warnings.

## API Evidence

`api/live-api-before-sb33.json` records the unchanged 5032 instance before this build was deployed. It shows chat session/workspace calls at about 15 seconds, project-structure calls below 100 ms, and the `Run app` `.NET runtime` node without typed metadata or action capabilities.

