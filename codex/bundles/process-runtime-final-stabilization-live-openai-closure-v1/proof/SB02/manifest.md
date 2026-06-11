# SB02 Proof Manifest

## Status
- Subbundle: SB02
- Status: Completed with live-provider blocker
- Owned requirements: REQ-003, REQ-004
- Raw notes: RN-002, RN-003
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Manifest
| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | `cf1e795e763ea53e5b08b47e345caefa755ea8565057875d503e838c52e68ed3` | `70ef68244c76fc519ab5a1d9126beffe470b63a20edabe5e1b3e7a49523337d8` |
| `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs` | `23648720b19edc5f022cc0f234e92b2975677bc128eb4e322d5bc096d544cdeb` | `072f9afd887b7aa4a4a952e49d5d868d10d8b01654c269db74d1c4711e5d0dd5` |

## Command Transcripts
- Live smoke initial failure: `bundle://proof/SB02/transcripts/live-openai-smoke-initial-failure.txt`
- Live smoke diagnostic failure: `bundle://proof/SB02/transcripts/live-openai-smoke-diagnostic.txt`
- Live smoke provider-exception diagnostic failure: `bundle://proof/SB02/transcripts/live-openai-smoke-diagnostic-with-provider-exception.txt`
- Passing diagnostic guard: `bundle://proof/SB02/transcripts/provider-diagnostic-guard-test.txt`
- Failing-first source assertion: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt`
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Live failure classification: `bundle://proof/SB02/transcripts/live-openai-classification.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Live Command Settings
- `OPENAI_API_KEY`: present-redacted
- `CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION`: `true`
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`: `true`
- `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL`: `5.4-mini`
- `CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS`: `180`
- `CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS`: `100000`

## Classification
- Result: live-provider-blocked.
- Provider/API failure: OpenAI Responses rejected requested model `5.4-mini`.
- Error proof: `bundle://proof/SB02/transcripts/live-openai-smoke-diagnostic-with-provider-exception.txt` records HTTP 400 `model_not_found`.
- Not classified as PostgreSQL failure, run-start failure, assignment failure, dispatch-start failure, finalizer failure, artifact failure, readback failure, cleanup failure, or skipped proof.
- Exact fix path: choose an OpenAI model accepted by the configured Responses provider, set `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL` to that model, and rerun the same live smoke command.

## Source Assertions
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` persists sanitized provider exception diagnostics for provider activity failure.
- `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs` includes run id, step id, execution state, provider/model, usage diagnostics, and sanitized provider details in live smoke failures.

## Failing-First And Passing Proof
- Failing-first: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` exits non-zero because baseline `HEAD` lacks `SB02_INV_001` and `BuildProviderFailureDiagnostic`.
- Passing: `bundle://proof/SB02/transcripts/provider-diagnostic-guard-test.txt` exits zero and includes `Live_process_run_smoke_SB02_INV_001_provider_failure_diagnostics_include_sanitized_exception_detail`.

## Anti-Stub Audit
- `bundle://proof/SB02/transcripts/anti-stub-audit.txt` reports no `TODO`, `NotImplemented`, or `fixture-specific` markers in SB02 changed files.

## Browser Or Host Proof
- N/A. SB02 has no browser-visible behavior.

## Downstream Smoke
- SB03 may proceed because SB02 produced an exact live-provider blocker and did not report skipped live proof as pass.
