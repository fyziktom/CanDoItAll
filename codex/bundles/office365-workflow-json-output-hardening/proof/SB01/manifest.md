# SB01 Proof Manifest

## Status

- Subbundle: `SB01-runtime-json-contract-hardening`
- Closure decision: `Completed`

## Changed Files And Hashes

- `bundle://proof/SB01/changed-file-hashes.txt`

| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs` | `89EE380A40DB70CBA5F01E2CEA0238F1CCD021802A889AE835C7FA8025609E90` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | `9473D1E0E39D84682E9C8DD254B2F4F4C418DAEB4CEC491C1B6DA7E475F161F1` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | `58349824427F702E63CC3CA2AF9B099DF15443ADBA15F34A7FEF3BC140D175FC` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | `E1781ACD98F83FFFA234AC9326E9D3220AB0906E0125DDF1CF8AA95CC6516FB3` |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | `42F2B85FCC4E9965ACA60FD8AE2A4594A0C7CA2C095D6499FB86149AF1A87572` |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs` | `63CFF122B6455A07AA922CDCEB20E39008D12BB261E565580C17406F6AEFFC5D` |
| `repo://tests/CanDoItAll.Tests.Integration/MafAgentRuntimeTests.cs` | `6139CD578153C28F5FEBF84DD64445550A42B9EC658763219E857FE568E70859` |

## Command Transcripts

- Failing-first: `bundle://proof/SB01/transcripts/failing-first-json-response-format-tests.txt`
- Passing targeted unit tests: `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt`
- Passing response-format application tests: `bundle://proof/SB01/transcripts/passing-response-format-application-tests.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Source Assertions

- `bundle://proof/SB01/source-assertions/response-format-source-assertions.txt`
- `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs` defines response-format option fields.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` populates those fields for JSON-required workflow LLM components and still calls `ValidateJsonPayload`.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` applies `ChatResponseFormat.Json` or `ChatResponseFormat.ForJsonSchema(...)`.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` rejects JSON response-format requests on providers that cannot support structured output.

## Semantic Invariants

- Semantic adequacy evidence: `bundle://proof/SB01/semantic-invariants.md`
- INV-SB01-001: JSON-required workflow LLM components set response-format options.
- INV-SB01-002: MAF `ChatOptions.ResponseFormat` receives schema-backed or generic JSON response format.
- INV-SB01-003: malformed returned JSON still fails.
- INV-SB01-004: project scope remains preserved.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| JSON response-format runtime options | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | Produced for each JSON-required workflow LLM call, applied to provider run options, then strict payload validation remains after response. | `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt` |

## Gate Result

- Entry gate: passed after prepared-stage validation.
- Closure gate: passed. SB02 may proceed.
