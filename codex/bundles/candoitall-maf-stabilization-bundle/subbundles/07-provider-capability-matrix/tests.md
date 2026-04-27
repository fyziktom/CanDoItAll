# Test Plan: 07 - Provider Capability Matrix and Runtime Gating


Unit tests:
- OpenAI Responses provider resolves expected capabilities.
- Azure OpenAI Responses provider resolves expected capabilities.
- Chat-completions provider resolves only capabilities actually supported by installed MAF adapter.
- Ollama/local provider does not receive unsupported hosted tools or structured-output assumptions.
- Unsupported capability request fails before runtime execution.

Integration tests:
- A structured-output process step refuses to run with a provider that cannot enforce structured output unless a validated fallback path is configured.
- Hosted tools attach only for supported providers.


## Minimum validation commands

Use the repository's actual test projects. At minimum attempt:

```bash
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If full-solution tests are too slow or environment-limited, run focused tests and explain exactly what was not run.
