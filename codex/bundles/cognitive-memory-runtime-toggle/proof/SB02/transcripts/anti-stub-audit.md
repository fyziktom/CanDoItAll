# SB02 Anti-Stub Audit Transcript

- Invariant ID: `CM-SB02-001`

Command:

```powershell
rg -n "Assert\.Empty\(orchestrator\.Requests\)|Assert\.Empty\(ingestion\.Requests\)|Assert\.Empty\(consolidation\.Requests\)|CognitiveMemoryRuntimeUsage\.DisabledReason" tests\CanDoItAll.Tests.Unit -S
```

ExitCode: 0

Output:

```text
tests\CanDoItAll.Tests.Unit\AgentContextContributionTests.cs:294:        Assert.Equal(CognitiveMemoryRuntimeUsage.DisabledReason, result.TraceMetadata["reason"]);
tests\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs:245:        Assert.Empty(ingestion.Requests);
tests\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs:246:        Assert.Empty(consolidation.Requests);
```

Audit conclusion: no permissive stubs; recording fakes expose accidental downstream calls.
