# Live OpenAI Policy

## Required live command shape

PowerShell example:

```powershell
$env:CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION = "true"
$env:CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE = "true"
$env:CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL = "5.4-mini"
$env:CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS = "180"
$env:CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS = "100000"

dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj `
  --configuration Debug --no-restore `
  --filter FullyQualifiedName~LiveProcessRunOpenAiSmokeIntegrationTests `
  --logger "console;verbosity=normal"
```

## Notes
- `OPENAI_API_KEY` must be detected as present but never printed.
- Use model `5.4-mini` for this closure smoke unless local provider configuration rejects that exact model.
- Timeout must remain within the test bounds.
- Token ceiling may be up to `100000` for this smoke, following the user correction that the previous bundle's `10000` cap can be 10x larger.
- A skipped live test is not live proof.
