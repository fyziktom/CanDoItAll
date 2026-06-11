# SB03 Semantic Invariants

- Invariant ID: SB03_INV_001
- Source raw note: NOTE-003 and NOTE-004.
- Expected behavior: `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL` is optional; absent override uses managed provider `DefaultModel`, then first non-empty suggested model, and fails as `provider-default-missing` only when provider models are absent.
- Disallowed shallow implementation: Requiring the model environment variable, silently hardcoding a model, or hiding missing provider defaults is rejected.
- Failing-first test: bundle://proof/SB03/transcripts/adversarial-old-required-model-policy-absent.txt proves the old required-model policy token is absent with a non-zero `rg` result.
- Passing test: bundle://proof/SB03/transcripts/focused-model-policy-tests.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs and bundle://proof/SB03/transcripts/model-policy-source-assertions-and-hashes.txt.
- Red-team negative case: bundle://proof/SB03/transcripts/adversarial-old-required-model-policy-absent.txt and `Live_process_run_smoke_SB03_INV_007_fails_as_provider_default_missing_without_default_or_suggestions`.
- Downstream dependency check: SB04 live proof shows `ModelSource=ProviderDefault`.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing live smoke model resolution | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB03/transcripts/focused-model-policy-tests.txt | bundle://proof/SB03/transcripts/adversarial-old-required-model-policy-absent.txt |
