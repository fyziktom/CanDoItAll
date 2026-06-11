# SB02 Semantic Invariants

## Invariant SB02_INV_001
- Invariant ID: `SB02_INV_001`
- Source raw note: RN-003 requires a real OpenAI process-run smoke using explicit bounded env values, and RN-002 requires exact failure classification if it does not work.
- Expected behavior: The smoke runs with `5.4-mini`, timeout `180`, max tokens `100000`, and redacted API-key presence; if the provider rejects the model, the proof records a provider/API blocker with exact fix path rather than treating the run as skipped or a process runtime failure.
- Disallowed shallow implementation: Reporting live proof as passed or skipped when the command never reached provider execution, or reporting generic `Failed` without provider/model diagnostics.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` proves baseline `HEAD` lacks `SB02_INV_001` and `BuildProviderFailureDiagnostic`.
- Passing test: `bundle://proof/SB02/transcripts/provider-diagnostic-guard-test.txt` proves provider failure diagnostics include sanitized exception detail and redact OpenAI-style secrets.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` before SHA-256 `cf1e795e763ea53e5b08b47e345caefa755ea8565057875d503e838c52e68ed3`, after SHA-256 `70ef68244c76fc519ab5a1d9126beffe470b63a20edabe5e1b3e7a49523337d8`; `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs` before SHA-256 `23648720b19edc5f022cc0f234e92b2975677bc128eb4e322d5bc096d544cdeb`, after SHA-256 `072f9afd887b7aa4a4a952e49d5d868d10d8b01654c269db74d1c4711e5d0dd5`.
- Production assertions: `bundle://proof/SB02/transcripts/live-openai-smoke-diagnostic-with-provider-exception.txt` shows process run id, step run id, provider `OpenAI default`, model `5.4-mini`, usage status `MissingAfterProviderActivity`, and HTTP 400 `model_not_found`; `bundle://proof/SB02/transcripts/source-assertions.txt` proves diagnostics are persisted through production runtime code.
- Red-team negative case: `bundle://proof/SB02/transcripts/live-openai-classification.txt` rejects skipped-proof closure and classifies the failure as live-provider-blocked.
- Downstream dependency check: SB03 may proceed because deterministic runtime proof can still establish runtime stability while SB06 must carry the live-provider-blocked final decision unless a valid model rerun passes.
