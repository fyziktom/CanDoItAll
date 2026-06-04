# SB08 Semantic Invariants

## Invariants

- Invariant ID: SB08-INV-001
- Source raw note: RN08 - Close MAF 1.6 runtime proof debt and tool/skill regression gaps.
- Expected behavior: MAF runtime proof is split into named tool-loop, context provider, finalizer, errors, approvals, MCP, A2A, workflow mapping, and trace-correlation slices, and each slice has command-backed proof before SB15 or SB18 relies on the runtime.
- Disallowed shallow implementation: Prompt-only wording, docs-only runtime closure, one broad test summary without named slice evidence, stale MAF 1.0 package documentation, source-only proof for runtime behavior, UI-only hiding of errors, or hardcoded Blazor/Tetris/project/run/user paths in production code.
- Failing-first test: bundle://proof/SB08/transcripts/failing-first.txt proves stale MAF 1.0 package references are absent and runs adversarial A2A, approval, MCP, and workflow-depth tests.
- Passing test: bundle://proof/SB08/transcripts/passing.txt proves 17 focused unit tests and 51 focused integration tests pass.
- Named slice proof: bundle://proof/SB08/transcripts/tool-loop.txt; bundle://proof/SB08/transcripts/context-provider.txt; bundle://proof/SB08/transcripts/finalizer.txt; bundle://proof/SB08/transcripts/errors.txt; bundle://proof/SB08/transcripts/approvals.txt; bundle://proof/SB08/transcripts/mcp.txt; bundle://proof/SB08/transcripts/a2a.txt; bundle://proof/SB08/transcripts/workflow-mapping.txt; bundle://proof/SB08/transcripts/trace-correlation.txt.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Maf/README.md.
- Production assertions: MAF proof slices map to existing runtime sources and tests for tool-loop snapshots, context provider attachment, required/shadow finalizers, bounded error handling, approval-required tools, MCP payload bounding, A2A endpoint validation, workflow status/event mapping, and trace capture.
- Red-team negative case: Missing A2A bearer secrets, invalid A2A endpoints, incompatible approval continuation session state, unsupported provider-native browser search, browser MCP image payload leakage, and workflow handoff depth overflow all fail predictably in named proof transcripts.
- Downstream dependency check: SB15 can build test taxonomy around the named slices and SB18 can use them as release-readiness categories.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Named MAF proof taxonomy | repo://src/CanDoItAll.AgentFramework.Maf/README.md Runtime Proof Slices section; source proof bundle://proof/SB08/transcripts/source-assertions.txt | SB15 test taxonomy and SB18 release red-team | bundle://proof/SB08/transcripts/passing.txt and named slice transcripts prove each category has a command-backed result | bundle://proof/SB08/transcripts/failing-first.txt proves stale MAF 1.0 references do not remain as false documentation |
| Runtime tool/context/finalizer/error/approval/MCP behavior | repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs, repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs, repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs, and repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs | AgentFramework execution service and governed process dispatch | bundle://proof/SB08/transcripts/tool-loop.txt, context-provider.txt, finalizer.txt, errors.txt, approvals.txt, and mcp.txt prove runtime behavior by named slice | bundle://proof/SB08/transcripts/failing-first.txt proves adverse approval, MCP, and session cases remain guarded |
| A2A/workflow/trace behavior | repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/A2ARemoteAgentToolFactory.cs and repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs | Remote-agent attachment, handoff workflows, workflow evidence, and operator trace diagnostics | bundle://proof/SB08/transcripts/a2a.txt, workflow-mapping.txt, and trace-correlation.txt prove endpoint validation, workflow mapping, and trace evidence | bundle://proof/SB08/transcripts/failing-first.txt proves invalid endpoints and handoff-depth overflow fail predictably |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB08/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB08/transcripts/passing.txt.
- Named slice proof: bundle://proof/SB08/transcripts/tool-loop.txt; bundle://proof/SB08/transcripts/context-provider.txt; bundle://proof/SB08/transcripts/finalizer.txt; bundle://proof/SB08/transcripts/errors.txt; bundle://proof/SB08/transcripts/approvals.txt; bundle://proof/SB08/transcripts/mcp.txt; bundle://proof/SB08/transcripts/a2a.txt; bundle://proof/SB08/transcripts/workflow-mapping.txt; bundle://proof/SB08/transcripts/trace-correlation.txt.
- Source assertions: bundle://proof/SB08/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB08/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB08/transcripts/changed-file-hashes.txt.
