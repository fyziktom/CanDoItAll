# Tests Passing Transcript

- Invariant ID: `CM-SB01-001`
- Invariant ID: `CM-SB02-001`
- Test name: `AutomationSettingsService_PersistsScheduleAndSourceOptions`
- Test name: `Cognitive_memory_contributor_skips_before_project_scope_when_runtime_usage_is_disabled`
- Test name: `ScheduledAutomationRunner_SkipsBeforeDownstreamCallsWhenRuntimeUsageIsDisabled`

Command:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~CognitiveMemoryOperationalSettingsTests|FullyQualifiedName~CognitiveMemoryOperationalServicesTests"
```

ExitCode: 0

Output:

```text
Passed: 38
Failed: 0
```

Command:

```powershell
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CognitiveMemoryPageTests"
```

ExitCode: 0

Output:

```text
Passed: 2
Failed: 0
```

Command:

```powershell
dotnet build CanDoItAll.slnx --no-restore
```

ExitCode: 0

Output:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```
