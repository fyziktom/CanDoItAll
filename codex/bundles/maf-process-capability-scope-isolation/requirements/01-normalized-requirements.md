# Normalized Requirements

## Functional Requirements

| ID | Requirement | Acceptance |
| --- | --- | --- |
| REQ-MAF-001 | Common MAF workspace image analysis must be domain-neutral. | Default and wrapper prompts in common workspace tooling contain no software-delivery, UI-design, UI-state, browser-proof, Blazor, or screenshot-comparison assumptions. |
| REQ-MAF-002 | Development-specific image analysis must move to a development-owned package or process-owned instruction channel. | A dedicated development tool/provider/project or scoped process instruction owns UI screenshot analysis behavior; common MAF does not reference it. |
| REQ-MAF-003 | MAF runtime context must accept a typed scoped capability override. | `AgentRuntimeContextIntent` or an adjacent runtime execution contract carries validated deny/require/allow-only directives and scoped instruction fragments. |
| REQ-MAF-004 | Suppression must happen before capabilities enter agent context. | Suppressed skills/tools/MCPs/providers are absent from effective capability descriptors, attached tools, attached skills, MCP tool lists, and context sources except excluded diagnostics. |
| REQ-MAF-005 | Required capabilities must fail predictably when absent or denied. | Non-empty required capabilities are passed to the evaluator; missing/denied required capabilities block governed execution with actionable diagnostics. |
| REQ-MAF-006 | Process templates must expose per-step runtime scope in a strongly typed schema. | Template documents have validated step scope fields for capability directives and scoped instruction fragments. |
| REQ-MAF-007 | Process runtime assignments must persist effective step scope. | `ProcessRuntimeStepAssignment` and persistence entities store the effective scope used for dispatch. |
| REQ-MAF-008 | Process-to-MAF handoff must serialize scope safely. | `AgentFrameworkProcessExecutionAdapter` writes scope metadata; `ExecutionInvocationMetadata` validates and resolves it without fail-open behavior. |
| REQ-MAF-009 | Process prompts must be composed from the same scope contract that governs capabilities. | Scoped instructions are included only when their matching capability scope is valid and not denied by the same step. |
| REQ-MAF-010 | Runtime tool-provider suppression must be supported by stable provider identity. | Provider-generated descriptors expose provider key or implementation key so policies can target provider-level access. |
| REQ-MAF-011 | Existing process allowed-operation filtering must remain intact. | Existing allowed operation tests still pass; new policy layers do not weaken write/mutation/browser restrictions. |
| REQ-MAF-012 | End-to-end proof must cover a management-only step suppressing a development skill. | Test run or integration harness shows a normally assigned development skill is excluded from context for that step only. |

## Non-Functional Requirements

| ID | Requirement | Acceptance |
| --- | --- | --- |
| NFR-001 | Maintain strict boundaries. | Process core/template/runtime contracts do not reference MAF wrapper implementation projects. |
| NFR-002 | Use strong typing. | New directive kinds, selector target kinds, scope effects, and instruction attachment modes use enums/record structs, not loose string switches. |
| NFR-003 | No silent fallback. | Invalid scope policy, invalid selector, missing required capability, and parse failures produce errors or blocked diagnostics. |
| NFR-004 | Keep edits phased and minimal. | MAF foundation can merge before process schema changes; domain package migration follows only after generic contracts are stable. |
| NFR-005 | Preserve diagnostics. | Context manifests and execution logs include actionable scope, rule, selector, and process step identifiers with no sensitive data. |
