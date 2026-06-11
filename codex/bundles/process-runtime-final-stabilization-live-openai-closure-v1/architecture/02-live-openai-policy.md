# Live OpenAI Policy

## Required live command shape

PowerShell example:

```powershell
$env:CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION = "true"
$env:CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE = "true"
$env:CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL = "gpt-4.1-mini"
$env:CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS = "180"
$env:CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS = "10000"

dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj `
  --configuration Debug --no-restore `
  --filter FullyQualifiedName~LiveProcessRunOpenAiSmokeIntegrationTests `
  --logger "console;verbosity=normal"
```

## Notes
- `OPENAI_API_KEY` must be detected as present but never printed.
- If model `gpt-4.1-mini` is not valid in the local provider setup, Codex may use the repo's configured default model, but it must record the chosen model explicitly.
- Timeout must remain within the test bounds.
- Token ceiling should remain low enough for a smoke test. Use `10000` unless a source-backed reason requires another value.
- A skipped live test is not live proof.
