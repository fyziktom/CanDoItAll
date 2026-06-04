# SB08 Proof Manifest

## Status

Completed.

## Goal

Close MAF/tool/skill proof debt from prior blockers with named proof slices for tool-loop, context provider, finalizer, errors, approvals, MCP, A2A, workflow mapping, and trace correlation.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.AgentFramework.Maf/README.md | Updates MAF package/project references and documents the named runtime proof slices required by this closure gate. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |

## Proof-bearing Source Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj | Confirms MAF 1.6, A2A, Workflows, MCP, and OpenTelemetry package surface. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs | Runtime proof source for tool-loop, finalizer, error, approval, and trace response behavior. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs | Runtime proof source for approval wrappers, tool traces, finalizer traces, and tool execution wrapping. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs | Runtime proof source for approval continuation session compatibility. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs | Runtime proof source for MCP tool attachment and browser MCP payload bounding. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/A2ARemoteAgentToolFactory.cs | Runtime proof source for A2A endpoint validation and remote tool creation. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs | Runtime proof source for workflow compilation, status/event mapping, and workflow audit scope correlation. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs | Reflection proof for MAF 1.6 symbols and intentionally absent hidden/old symbols. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs | Integration proof for runtime tool, context, finalizer, error, approval, MCP, and trace slices. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs | Integration proof for MAF handoff workflow mapping and depth guard behavior. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Unit/A2ARemoteAgentToolFactoryTests.cs | Unit proof for A2A disabled, missing secret, and invalid endpoint rejection. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolInvocationResultTests.cs | Unit proof for tool invocation result and nested failure parsing. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs | Static regression proof for finalizer, approval, and domain-neutral hardening invariants. | bundle://proof/SB08/transcripts/changed-file-hashes.txt |

## Failing-first Or Adversarial Proof

- bundle://proof/SB08/transcripts/failing-first.txt records a non-zero search proving stale MAF 1.0 package references are not present and records adversarial A2A, approval-continuation, browser MCP payload, and workflow depth-guard tests.

## Passing Proof

- bundle://proof/SB08/transcripts/passing.txt records 17 passing focused unit tests and 51 passing focused integration tests for the MAF runtime closure surface.
- Named slice transcripts:
  - bundle://proof/SB08/transcripts/tool-loop.txt
  - bundle://proof/SB08/transcripts/context-provider.txt
  - bundle://proof/SB08/transcripts/finalizer.txt
  - bundle://proof/SB08/transcripts/errors.txt
  - bundle://proof/SB08/transcripts/approvals.txt
  - bundle://proof/SB08/transcripts/mcp.txt
  - bundle://proof/SB08/transcripts/a2a.txt
  - bundle://proof/SB08/transcripts/workflow-mapping.txt
  - bundle://proof/SB08/transcripts/trace-correlation.txt

## Source Assertions

- bundle://proof/SB08/transcripts/source-assertions.txt records README proof-slice documentation, MAF 1.6 package references, MAF 1.6 symbol reflection, runtime tests, A2A factory proof, handoff proof, workflow status/event mapping, and trace snapshot sources.

## Anti-stub Audit

- bundle://proof/SB08/transcripts/anti-stub-audit.txt records no TODO, pending, stub, or `NotImplementedException` markers in the SB08 changed README file.

## Changed-file Hashes

- SHA-256 `10252B3FC19060D4FE4D6A67B7736D85CC04576DA827462BC60102CE4CCD46AD` repo://src/CanDoItAll.AgentFramework.Maf/README.md
- bundle://proof/SB08/transcripts/changed-file-hashes.txt records hashes for the changed README plus proof-bearing MAF source and test files.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| MAF runtime proof slice map | repo://src/CanDoItAll.AgentFramework.Maf/README.md; source proof bundle://proof/SB08/transcripts/source-assertions.txt | SB15 proof-harness taxonomy and SB18 final release red-team | Names the nine proof slices required to avoid treating one broad runtime test as full MAF closure | Stale MAF 1.0 package references are absent; adversarial proof bundle://proof/SB08/transcripts/failing-first.txt |
| Tool, context, finalizer, error, approval, and MCP runtime behavior | repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs, repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs, repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs, and repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs | AgentFramework execution service, governed process automation, and runtime operator diagnostics | Each slice has passing transcript proof under bundle://proof/SB08/transcripts/ and broad proof in bundle://proof/SB08/transcripts/passing.txt | Missing/incompatible approval session, unusable approval path, unsupported provider-native tool, browser MCP image payload, and bounded-error cases are covered by bundle://proof/SB08/transcripts/failing-first.txt |
| A2A and workflow mapping runtime behavior | repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/A2ARemoteAgentToolFactory.cs and repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs | Remote-agent tool attachment, MAF handoff workflows, workflow runtime evidence, and downstream process automation | A2A endpoint validation and workflow status/event mapping are covered by bundle://proof/SB08/transcripts/a2a.txt and bundle://proof/SB08/transcripts/workflow-mapping.txt | Missing A2A bearer secrets, invalid endpoints, and handoff depth overflow fail predictably; proof bundle://proof/SB08/transcripts/failing-first.txt |
| Trace correlation runtime evidence | repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs, repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs, and repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs | Execution run records, finalizer validation, tool receipt audit, workflow progress, and SB13/SB18 diagnostics | Tool invocation traces, finalizer invocations, workflow audit scope, and OpenTelemetry dependency proof are captured by bundle://proof/SB08/transcripts/trace-correlation.txt | Trace proof requires source and test citations; it is not closed by README wording alone per bundle://proof/SB08/transcripts/source-assertions.txt |

## Browser Validation

N/A. SB08 changed documentation and command-backed runtime proof only. It did not change Agent Framework UI markup, CSS, route wiring, layout, or visible UI rendering components.

## Closure

- SB08-INV-001 is satisfied by bundle://proof/SB08/transcripts/passing.txt and the nine named slice transcripts.
- MAF 1.6 adoption and stale 1.0 reference rejection are recorded by bundle://proof/SB08/transcripts/failing-first.txt and bundle://proof/SB08/transcripts/source-assertions.txt.
- SB15 and SB18 may rely on the named proof categories after this gate.
