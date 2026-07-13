# Semantic Invariants - SB03

## INV-SB03-01

- Invariant ID: `INV-SB03-01`
- Source raw note: F01/F10 require persisted structured process results for process-bound AgentFramework runs.
- Expected behavior: process result summaries persist validated structured outcome data for exact step correlation.
- Disallowed shallow implementation: storing only raw prose result summaries.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs`.
- Production assertions: exact observation contracts carry structured status, branch, artifacts, next actions, and diagnostics.
- Red-team negative case: unparseable prose-only summaries cannot satisfy the exact blocked packet evidence requirement.
- Downstream dependency check: SB06 and SB09 use durable structured outcomes for artifact truth and regression proof.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Structured result summary | AgentFramework execution persistence | observation reader/projection diagnostics | execution run completion/failure lifecycle | raw prose-only result is not enough |
