# SB01 Semantic Invariants

## Invariants

- Invariant ID: `INV-SB01-001`
- Source raw note: N001.
- Expected behavior: JSON-required workflow LLM components set response-format execution options before the provider call.
- Disallowed shallow implementation: prompt-only wording changes that leave runtime options empty.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-json-response-format-tests.txt`.
- Passing test: `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs` and `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`.
- Production assertions: `bundle://proof/SB01/source-assertions/response-format-source-assertions.txt`.
- Red-team negative case: prompt-only hardening cannot pass because tests assert runtime response-format options.
- Downstream dependency check: SB02 live workflow execution depends on this before `summarize-office365`.

- Invariant ID: `INV-SB01-002`
- Source raw note: N001.
- Expected behavior: MAF run options apply schema-backed or generic JSON response format.
- Disallowed shallow implementation: storing schema text without assigning `ChatOptions.ResponseFormat`.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-json-response-format-tests.txt`.
- Passing test: `bundle://proof/SB01/transcripts/passing-response-format-application-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` and `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`.
- Production assertions: `bundle://proof/SB01/source-assertions/response-format-source-assertions.txt`.
- Red-team negative case: a schema that is never applied to provider run options cannot satisfy the integration assertion.
- Downstream dependency check: Office365 workflow execution uses the same MAF run options path.

- Invariant ID: `INV-SB01-003`
- Source raw note: N001.
- Expected behavior: malformed returned JSON still fails in `ValidateJsonPayload`.
- Disallowed shallow implementation: extracting, trimming, repairing, or accepting the first valid JSON fragment.
- Failing-first test: original raw failure in `bundle://inputs/00-original-request.md`.
- Passing test: `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`.
- Production assertions: `bundle://proof/SB01/source-assertions/response-format-source-assertions.txt`.
- Red-team negative case: `{"markdown":"ok"} + invalid` must throw.
- Downstream dependency check: project-structure storage only receives parser-valid JSON.

- Invariant ID: `INV-SB01-004`
- Source raw note: N003.
- Expected behavior: project scope handling remains unchanged while response-format options are added.
- Disallowed shallow implementation: dropping `ContextWorkspaceScope`, `projectId`, `nodeId`, or run context while adding JSON options.
- Failing-first test: existing regression risk documented by `repo://codex/bundles/office365-email-summary-project-scope-fix`.
- Passing test: `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` and `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`.
- Production assertions: `bundle://proof/SB01/source-assertions/response-format-source-assertions.txt`.
- Red-team negative case: response-format additions must not regress project-scope propagation.
- Downstream dependency check: SB02 proves `projectId`, `nodeId`, and Office365 run context survive the live workflow.

| Invariant ID | Source raw note | Failing-first test | Passing test | Changed source files | Production assertions | Red-team negative case | Downstream dependency check |
| --- | --- | --- | --- | --- | --- | --- | --- |
| INV-SB01-001 | N001 | `bundle://proof/SB01/transcripts/failing-first-json-response-format-tests.txt` showed missing execution-option fields before implementation. | `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt` proves schema and generic JSON options are passed. | `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`; `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | A workflow LLM component whose model settings require JSON or whose result shape is `Json` sets `RequireJsonResponseFormat` before calling MAF. | A prompt-only change would leave runtime response-format options empty. | SB02 relies on this before rerunning `summarize-office365`. |
| INV-SB01-002 | N001 | `bundle://proof/SB01/transcripts/failing-first-json-response-format-tests.txt` compiled-failed before response-format fields existed. | `bundle://proof/SB01/transcripts/passing-response-format-application-tests.txt` proves `ChatOptions.ResponseFormat` receives schema-backed response format. | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`; `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | The runtime applies `ChatResponseFormat.ForJsonSchema(...)` when a component schema exists and `ChatResponseFormat.Json` when no schema exists. | Merely storing schema text without using it in `ChatOptions.ResponseFormat` would fail this invariant. | Provider run options are the boundary SB02 needs for actual Office365 workflow execution. |
| INV-SB01-003 | N001 | Original raw failure in `bundle://inputs/00-original-request.md` and failing-first test context prove malformed output was the user-facing defect. | `MafWorkflowLlmComponentInvokerRejectsInvalidJsonWithoutRepairingPayload` in `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt`. | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | Malformed JSON returned by a workflow LLM node still fails in `ValidateJsonPayload`. | Payload `{"markdown":"ok"} + invalid` must throw instead of being extracted, trimmed, or repaired. | Downstream project-structure storage only receives parser-valid JSON. |
| INV-SB01-004 | N003 | Existing project-scope regression risk from `repo://codex/bundles/office365-email-summary-project-scope-fix`. | `MafWorkflowLlmComponentInvokerPassesProjectScopeFromWorkflowPayload` in `bundle://proof/SB01/transcripts/passing-targeted-json-response-format-tests.txt`. | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`; `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs` | Project scope handling remains unchanged while JSON response-format options are added. | Adding response-format fields must not drop `ContextWorkspaceScope` or project scope payload. | SB02 validates `projectId`, `nodeId`, and Office365 run context through the live project-structure workflow. |

## Semantic Adequacy Gate

- Shallow-pass trap: prompt-only changes can still let malformed prose or concatenated output reach `ValidateJsonPayload`.
- Adversarial negative proof: `MafWorkflowLlmComponentInvokerRejectsInvalidJsonWithoutRepairingPayload` sends `{"markdown":"ok"} + invalid` and verifies the runtime throws instead of repairing.
- Semantic positive proof: response-format tests verify both schema-backed and generic JSON-required workflow components set response-format execution options, and integration tests verify MAF `ChatOptions.ResponseFormat` receives the schema.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` contains no matches in the changed diff for `TODO`, `NotImplemented`, fixture-specific fallback, JSON extraction, or repair behavior.
- Raw-note literal closure: N001 is closed for runtime hardening because malformed JSON is now prevented upstream through response-format options and still rejected downstream if returned.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| JSON response-format runtime options | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | Created per workflow LLM invocation, applied to MAF run options, then validated by existing JSON parser. | `MafWorkflowLlmComponentInvokerRejectsInvalidJsonWithoutRepairingPayload` proves invalid returned JSON still fails. |
