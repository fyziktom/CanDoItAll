# SB07 Semantic Invariants

- Invariant ID: `SB07-INVARIANT-001`
- Source raw note: `RQ-008` The Processes-owned runtime tool provider must be maintainable without extracting process core or changing its public tool surface.
- Expected behavior: ProcessAgentRuntimeToolProvider remains registered from the Processes module, still exposes the same 23 process tool names, keeps access denial and approval-policy behavior unchanged, and is split into smaller source files with a static guard against returning to a 900+ line monolith.
- Disallowed shallow implementation: Moving methods into files while dropping tool names, weakening read/write/definition access checks, changing policy classification, or adding no regression guard for file size.
- Failing-first test: `AgentRuntimeToolProviderArchitectureTests.ProcessAgentRuntimeToolProvider_split_files_stay_below_monolith_threshold` fails if the provider split count or line-count threshold regresses.
- Passing test: `bundle://proof/SB07/transcripts/process-provider-unit-tests.txt`, `bundle://proof/SB07/transcripts/process-runtime-provider-integration-tests.txt`, `bundle://proof/SB07/transcripts/process-provider-access-denial-test.txt`, `bundle://proof/SB07/transcripts/agent-tool-invocation-policy-tests.txt`, and `bundle://proof/SB07/transcripts/agent-capability-evaluator-test.txt`.
- Changed source files: `bundle://proof/SB07/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB07/source-assertions/process-provider-split-source-assertions.txt`.
- Red-team negative case: The parity test would fail if any process tool name were added, removed, or renamed; the access test would fail if read/write/definition scope denial regressed; the policy tests would fail if process tool approval classification drifted.
- Downstream dependency check: SB08 may start because SB07 did not change tool semantics and the provider is now small enough for purpose/access hardening without compounding monolith risk.
