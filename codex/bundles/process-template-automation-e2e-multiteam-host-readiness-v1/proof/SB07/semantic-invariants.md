# SB07 Semantic Invariants

- Invariant ID: `SB07_INV_001`
- Source raw note: Continue scheduler/workflow readiness without driver side effects.
- Expected behavior: Scheduler and workflow jobs run through `IProcessReadOnlyVerificationJobRunner`, complete with lifecycle status, source reference, correlation id, request identity, audit reference, manager readback contract, audit records, and no mutation flags.
- Disallowed shallow implementation: checking only constructor values or bypassing the manager verification facade.
- Failing-first test: `bundle://proof/SB07/transcripts/failing-first-source-assertion.txt` shows the baseline lacked the SB07 traceable lifecycle/readback invariant name and workflow-branch contract assertions.
- Passing test: `bundle://proof/SB07/transcripts/focused-test.txt` shows the focused SB07 runner test passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`.
- Production assertions: `ProcessReadOnlyVerificationJobRunner` builds lifecycle state from manager readback and returns `ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob`.
- Red-team negative case: unsupported source kinds are rejected by `ProcessReadOnlyVerificationJob`; execution-capable side effects are not exposed.
- Downstream dependency check: SB08 can close release readiness with scheduler/workflow host proof included.
