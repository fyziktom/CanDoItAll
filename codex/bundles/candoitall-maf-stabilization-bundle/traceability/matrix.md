# Requirement Traceability Matrix

| Requirement | Subbundles | Test expectation |
|---|---|---|
| R01 typed DTO contracts | 03, 04, 10 | Machine-critical DTOs have validators and tests. |
| R02 preserve structured output | 02, 03 | Approval continuation tests prove `ResponseFormat` remains configured. |
| R03 validate before success | 03 | Invalid JSON/schema/business output cannot complete run. |
| R04 validators | 03 | Validator registry tests for each critical DTO. |
| R05 repair/retry | 03 | Invalid output triggers bounded repair; retry limit enforced. |
| R06 pre-execution tool governance | 01 | Function invocation middleware blocks or approves before tool execution. |
| R07 honor tool enabled flags | 01 | Disabled built-in tools are not attached. |
| R08 finalizer tools | 04 | Missing/multiple/malformed finalizer calls fail. |
| R09 process state source of truth | 06 | Session restore tests prove process state remains explicit. |
| R10 MAF workflow alignment | 05 | MAF workflow harness executes selected process subflow and resumes from checkpoint. |
| R11 provider capability matrix | 07 | Unsupported features fail early with clear errors. |
| R12 domain-neutral runtime | 09 | No calculator-specific text in generic runtime. |
| R13 observability | 08 | Logs/traces include policy/validation/finalizer outcomes. |
| R14 tests | all | Build and focused test reports attached by Codex. |
