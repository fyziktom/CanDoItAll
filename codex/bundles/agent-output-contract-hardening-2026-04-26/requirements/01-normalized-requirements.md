# Normalized Requirements

| ID | Requirement | Source | Owner | Proof |
| --- | --- | --- | --- | --- |
| REQ-001 | Inventory Agent Framework creation, execution, response parsing, prompt-only JSON, function tools, and process-state updates. | User audit scope | 01 | Audit report names files and unsafe patterns. |
| REQ-002 | Add typed DTO contracts for process-critical agent outcomes and reusable validation results. | User typed contracts | 02 | Serialization and validator tests pass. |
| REQ-003 | Configure structured output through `ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()` for typed critical run requests where supported. | User structured output | 03 | Tests prove run options carry a typed schema. |
| REQ-004 | Add a bounded validation/repair/failure path so invalid outputs cannot be accepted silently. | User repair/retry | 03 | Tests prove retry limit and typed failure/escalation behavior. |
| REQ-005 | Replace process-step workflow decisions from markdown comments with validated typed outcome data. | User machine contract | 04 | Tests prove raw markdown cannot approve or branch a workflow step. |
| REQ-006 | Preserve raw output diagnostics without logging secrets or unvalidated sensitive payloads. | User observability | 03, 04 | Audit and tests cover raw capture/hash or redaction fields. |
| REQ-007 | Document the output-contract architecture and how to add new contracts. | User documentation | 05 | `docs/agent-output-contracts.md` exists and covers expected topics. |
| REQ-008 | Run relevant build/test validation and record failures honestly. | User quality bar | 05 | Execution report contains commands and outcomes. |
