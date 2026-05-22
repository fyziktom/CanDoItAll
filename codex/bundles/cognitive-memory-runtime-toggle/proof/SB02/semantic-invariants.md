# SB02 Semantic Invariants

- Invariant ID: `CM-SB02-001`
- Source raw note: `N001`, `N002`, `N005`, and `N006`.
- Expected behavior: disabled Cognitive Memory skips agent context, memory workflow executors, and scheduled automation before optional memory work or project-scope failures.
- Disallowed shallow implementation: catching exceptions, unregistering at startup, or disabling only one integration point.
- Failing-first test: N/A process because the runtime log is the failure artifact; guard tests reproduce the missing-scope shape.
- Passing test: `Cognitive_memory_contributor_skips_before_project_scope_when_runtime_usage_is_disabled` and `ScheduledAutomationRunner_SkipsBeforeDownstreamCallsWhenRuntimeUsageIsDisabled`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` and `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs`.
- Production assertions: disabled calls return explicit skipped results; enabled calls keep strict error semantics; runtime settings are read per call.
- Red-team negative case: disabled mode receives no project scope and invalid scheduled automation inputs, yet no recall, ingestion, or consolidation call occurs.
- Downstream dependency check: agent chat, workflow runtime, and scheduled automation all consume the same persisted setting and return deterministic skip payloads.
