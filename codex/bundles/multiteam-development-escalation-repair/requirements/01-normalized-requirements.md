# Normalized Requirements

| Id | Requirement | Observable success criteria |
| --- | --- | --- |
| R1 | Diagnose the current 5032 Calculator multiteam process run and identify the real escalation cause. | The execution report names the failing run ids, failing step keys, assigned roles, allowed operations, target scopes, and why the runtime loop was possible. |
| R2 | Enforce process-role separation. | Architect and architecture-review steps remain read-only and cannot receive product mutation operations; implementation and repair steps are the only product-mutable lanes. |
| R3 | Repair operation contracts for implementation subprocesses. | Steps that must launch subprocesses explicitly have external-action/start-process allowances; steps that must mutate code have `MutateProductTarget` and mutable target scope; QA steps have validation/runtime/browser proof allowances where required. |
| R4 | Improve HR/readiness matching so it catches missing capabilities before run execution. | Tests prove readiness rejects or flags assignments when required operations/tools for a step are absent instead of accepting the agent with a generic "workspace tool readiness" reason. |
| R5 | Keep multiteam development split into small, testable subprocesses. | Template boundaries remain `software-delivery` -> `dotnet-development-slice` -> bounded child implementation, with tests documenting which step owns subprocess launch versus direct product mutation. |
| R6 | Load repaired templates into the development DB/runtime. | The restarted 5032 instance reports current template hash/plan that includes the repaired contracts. |
| R7 | Prove the fix with real process runs. | A fresh 5032 run for the simple Calculator scenario reaches the fixed implementation/validation path without repeating the prior false escalation loop, or records a concrete external blocker unrelated to the repaired contract. |
