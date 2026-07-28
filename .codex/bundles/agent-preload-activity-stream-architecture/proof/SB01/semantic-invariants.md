# SB01 Semantic Invariants

## SB01-INV-01 — Reproducible startup operation baseline

- Invariant ID: `SB01-INV-01`
- Source raw note: loading appears frozen before tool initialization and must be deeply understood and measured before optimization.
- Expected behavior: the no-provider harness records ordered milestones and strongly typed catalog/provider/session/run-summary/run-detail operation counts for cold/warm and new/existing-session paths.
- Disallowed shallow implementation: a stopwatch around an immediate fake returning no persistence work, or prose-only source counting.
- Failing-first test: N/A; this invariant characterizes current non-production baseline behavior and does not claim a production behavior change.
- Passing test: `bundle://proof/SB01/transcripts/passing-startup-baseline-matrix.txt` records three independent iterations and 12/12 passing rows; `bundle://proof/SB01/transcripts/passing-startup-characterizations.txt` records the existing-session path.
- Changed source files: `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`, LF-normalized UTF-8 before `A275729F3870B34C520B5672F390CC66BD74E059CA9CC79B419D115355F915E4`, after `81C4D8F43188EE095CCF7993EA553A057D5C859EEABE2D9E06323287EEC2C5CC`.
- Production assertions: `bundle://proof/SB01/source-assertions.md`, `bundle://proof/SB01/constructor-query-inventory.md`, and `bundle://proof/SB01/transcripts/manual-factory-wiring-source-assertion.txt` bind the recorded calls to the shared Core workspace execution/store/registry path and constrain the measurement claim.
- Red-team negative case: preparation warm-up must not be reported as an execution improvement when the send path does not consume it.
- Downstream dependency check: SB02/SB05 comparisons use this same harness and operation vocabulary.

## SB01-INV-02 — Persistence-gated throwing compatibility event

- Invariant ID: `SB01-INV-02`
- Source raw note: the UI is silent/frozen before later persisted initialization messages and event architecture must not corrupt execution.
- Expected behavior: the current characterization records that Planning is persisted before `ExecutionUpdated`; a throwing subscriber propagates, prevents later sink/runtime execution, and leaves persisted Planning state.
- Disallowed shallow implementation: manually invoking a consumer or asserting only that an exception occurred.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-execution-updated-isolation.txt` proves the desired isolation assertion fails against current production behavior after canonical Planning persistence.
- Passing test: `bundle://proof/SB01/transcripts/passing-startup-characterizations.txt` proves the current defect contract, including the final exact-state rerun.
- Changed source files: integration test LF-normalized hashes as recorded in `SB01-INV-01`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` identifies the production persistence, compatibility-event, event-sink, and runtime ordering.
- Red-team negative case: prove the event sink and runtime were not reached while persisted state exists.
- Downstream dependency check: SB02 must reverse the failure coupling without changing durable store outcome.

## SB01-INV-03 — Preparation pool is not execution preparation

- Invariant ID: `SB01-INV-03`
- Source raw note: prepared instances/data should make agent startup faster.
- Expected behavior: the current characterization records that warming/acquiring the existing pool returns metadata but does not change send-path storage operation counts.
- Disallowed shallow implementation: timing only `AgentChatPreparationPool.AcquireAsync` and claiming agent execution is warm.
- Failing-first test: N/A; this current non-production characterization uses the adversarial warm/cold comparison after setup counters are reset.
- Passing test: `bundle://proof/SB01/transcripts/passing-preparation-single-flight.txt` proves same-key single-flight, and `bundle://proof/SB01/transcripts/passing-startup-baseline-matrix.txt` proves warm rows retain the cold send-path counts.
- Changed source files: `repo://tests/Unit/CanDoItAll.Tests.Unit/FloatingAgentChatArchitectureTests.cs`, LF-normalized UTF-8 before `79706377EF268E7B8347A988AFCAD7F8F5B6AB45C54BEA3DD272AC012B73A2AF`, after `A6D20FE6AA089CA034504ED51B1A5C5CD3DF75ADC6C422C9E939F51110DC41DD`; integration test hashes as recorded in `SB01-INV-01`.
- Production assertions: `bundle://proof/SB01/source-assertions.md` confirms that the current send path never consumes `IAgentChatPreparationPool`.
- Red-team negative case: compare warm and cold send counts after resetting setup counters.
- Downstream dependency check: SB03 must integrate validated blueprints into execution rather than rename the existing pool.

## SB01-INV-04 — No paid provider baseline

- Invariant ID: `SB01-INV-04`
- Source raw note: agent tests must use `gpt-5.4-mini`, not Terra, to control cost.
- Expected behavior: SB01 replaces `IAgentRuntime` and makes no provider-backed model call.
- Disallowed shallow implementation: configuring a cheap model while still sending a network request during deterministic baseline tests.
- Failing-first test: N/A; this current non-production safety constraint is enforced by DI source assertions and fail-closed runtime methods rather than a production behavior change.
- Passing test: `bundle://proof/SB01/transcripts/anti-stub.txt` and the final fail-closed rerun in `bundle://proof/SB01/transcripts/passing-startup-baseline-matrix.txt`.
- Changed source files: integration test LF-normalized hashes as recorded in `SB01-INV-01`.
- Production assertions: N/A.
- Red-team negative case: `StartupBarrierAgentRuntime.TestProviderAsync`, `RunProviderTestChatAsync`, and `CreateOrUpdateProviderModelAsync` each throw `NotSupportedException`; the startup matrix passes only if none is invoked.
- Downstream dependency check: the single real `gpt-5.4-mini` validation remains SB07 only.
