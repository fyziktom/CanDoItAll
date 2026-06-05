# SB16 Semantic Invariants

- Invariant ID: SB16-INV-001
- Source raw note: Preserve original functionality while isolating execution launch and failed-run inspection.
- Expected behavior: Execution requests still use the same process-step source, correlation, trigger causation, system requester, finalizer-required policy, governed structured output, and auto-approved tool calls; failed executions still fetch detail and choose preferred response text; success/adopted/failed launch paths still produce one attempt snapshot.
- Disallowed shallow implementation: A helper that changes source metadata, disables finalizer validation, drops structured output kind, hides side effects, skips failed detail fetch, or changes preferred failed response text is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; bundle://proof/SB16/transcripts/focused-launch-failure-tests.txt proves launch metadata and failure-classification behavior still pass.
- Passing test: bundle://proof/SB16/transcripts/focused-launch-failure-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionInvocationRequestBuilder.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionAttemptResultNormalizer.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessFailedExecutionInspectionCoordinator.cs, and dispatcher wrappers listed in bundle://proof/SB16/manifest.md.
- Production assertions: bundle://proof/SB16/transcripts/source-assertions-and-scans.txt proves policy/source tokens, helper delegation, line-count movement, no hidden pure-helper side effects, no Core/driver tokens, and no stubs.
- Red-team negative case: bundle://proof/SB16/transcripts/source-assertions-and-scans.txt scans pure launch helpers for persistence, agent-save, transition, and journal side effects.
- Downstream dependency check: SB17-SB28 may proceed because post-attempt facts and retry rules now consume the same normalized attempt result shape.
