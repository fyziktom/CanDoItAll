# Normalized Requirements

| ID | Requirement | Acceptance signal |
|---|---|---|
| R01 | Every machine-critical agent output must have a typed top-level DTO contract. | No workflow/process decision is parsed from markdown or raw text. |
| R02 | Structured output configuration must be preserved across initial runs, approvals, retries, repairs, background responses, and continuations. | Tests cover manual approval continuation and auto-approved continuation with `ResponseFormat` still applied. |
| R03 | Agent output must be validated before success is persisted. | Invalid structured output cannot produce `RunOutcome.Succeeded` for machine-critical runs. |
| R04 | Concrete business validators must exist for machine-critical DTOs. | Validator registry includes process outcomes, patches, code review, architecture review, implementation plans, test plans, tool decisions, and generic step envelopes. |
| R05 | Invalid output must trigger bounded repair/retry or a typed failure/escalation. | Retry count is configurable and finite; repaired output is revalidated. |
| R06 | Dangerous/write/destructive tools must be governed before execution. | Function invocation middleware can block, require approval, sanitize, or log policy decisions before the tool body runs. |
| R07 | Tool enablement configuration must be honored. | Disabled built-in tools are not attached and are covered by tests. |
| R08 | Finalizer tools must exist for selected critical decisions. | Missing, multiple, or malformed finalizer calls fail validation. |
| R09 | Process state must remain the source of truth. | Session/history cannot be the only storage for process decisions, approvals, branch outcomes, or artifacts. |
| R10 | MAF workflow alignment must be incremental and non-disruptive. | Existing process behavior still passes, while selected subflows can run through MAF workflow/orchestration harnesses. |
| R11 | Provider/model capabilities must be explicit and enforced. | Unsupported structured output, hosted tools, approvals, background responses, or compaction fail early with clear diagnostics. |
| R12 | Runtime logic must be domain-neutral. | Generic MAF runtime no longer contains calculator-specific instructions. |
| R13 | Observability must explain agent behavior. | Logs/traces include agent id, step id, process id, tool policy decisions, validation errors, repair attempts, finalizer status, raw hash, and final outcome. |
| R14 | Tests must protect the stabilized architecture. | Unit, integration, and regression tests are added or updated for every changed runtime behavior. |
