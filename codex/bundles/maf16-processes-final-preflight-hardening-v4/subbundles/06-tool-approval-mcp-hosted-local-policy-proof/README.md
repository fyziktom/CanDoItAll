# SB06: 06-tool-approval-mcp-hosted-local-policy-proof

## Status

- Status: Blocked
- Behavior-changing: True until execution proves otherwise.

## Objective

Prove function, local MCP, hosted MCP, and runtime tools pass through CanDoItAll policy.

## Covered Inputs

- RQ04

## Prerequisites

- SB04 completed because tool loop proof must be trusted.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessToolOperationAuthorizer.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs

## Deliverables

- Implement or verify the behavior described by this subbundle.
- Update bundle://proof/SB06/manifest.md and bundle://proof/SB06/semantic-invariants.md when behavior or critical proof changes.
- Update bundle://reviews/01-execution-report.md with gate and proof results.

## Dependency Impact

- Downstream phases must reopen this subbundle if its proof is contradicted by later source or validation evidence.
- Critical foundation: requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Validation Depth

- Run the narrowest targeted tests that prove this subbundle, plus dependent smoke proof when this is a critical foundation.
- Capture command transcripts with Command: and ExitCode: fields under bundle://proof/SB06/transcripts/.
- Record changed-file hashes and source assertions after implementation.

## Implementation Steps

- Audit policy entry points for function, local MCP, hosted MCP, and shell/browser tools.
- Add or run negative proof for denied risky operations.
- Add or run positive proof for allowed governed operations.

## Scope Exceptions

- None planned. If execution discovers an unsupported path, record it here and in the execution report before closure.

## Do Not Do

- Do not replace missing proof with prose.
- Do not silently narrow all, every, must, or equivalent requirements.
- Do not run a full live process test before SB15 explicitly allows it.

## Acceptance Checklist

- Entry gate prerequisites are satisfied or explicitly blocked.
- Required implementation/proof steps are complete.
- Failing-first or adversarial proof is captured for behavior changes.
- Passing proof, source assertions, anti-stub audit, and changed-file hashes are captured.
- Execution report and raw-note closure rows are updated.

## Proof Required

- bundle://proof/SB06/transcripts/failing-first.txt
- bundle://proof/SB06/transcripts/passing.txt
- bundle://proof/SB06/transcripts/source-assertions.txt
- bundle://proof/SB06/transcripts/anti-stub-audit.txt
- bundle://proof/SB06/transcripts/changed-file-hashes.txt
- bundle://proof/SB06/manifest.md
- bundle://proof/SB06/semantic-invariants.md

## Browser Validation Logging

- N/A unless a browser tool policy proof is added; record host/tool proof in transcripts.

## Progression Gate

- Pass only when the acceptance checklist is complete and downstream dependencies can trust this subbundle without borrowing unstated assumptions.

## Suggested Agent Prompt

- Execute SB06 only. Keep changes scoped to its objective, capture artifact-backed proof, update status/report files, then run the closure gate before continuing.
