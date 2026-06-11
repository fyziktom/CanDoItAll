# SB04 Semantic Invariants

- Invariant ID: `SB04_INV_001`
- Source raw note: Restore reliable multi-team software-delivery process execution.
- Expected behavior: `software-delivery` completes the first-pass happy path through real dispatch: scope, architecture subprocess, implementation subprocess, peer review, QA accepted branch, security review, run-command writeback, screenshot writeback, release readiness approval, rollout, and post-release learning.
- Disallowed shallow implementation: catalog-only representative mapping, manually completed subprocesses, or chat-only browser proof.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt` shows the baseline lacked the SB04 E2E test.
- Passing test: `bundle://proof/SB04/transcripts/focused-test.txt` shows the focused SB04 E2E passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`, `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessSubprocessArtifactSourceResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`.
- Production assertions: inherited subprocess assignments are preferred before direct candidates, explicit child subprocess mappings may bridge artifact kinds, process-mock browser tools emit durable output filenames/files, and screenshot writeback records remain narrative artifacts.
- Red-team negative case: an implementation that only selects process-mock parent agents but loses inherited child subprocess agents would fail the SB04 E2E at subprocess execution.
- Downstream dependency check: SB05 may proceed because generic template automation support is no longer software-only.
