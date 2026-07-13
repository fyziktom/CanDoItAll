# Normalized Requirements

| ID | Requirement | Source | Acceptance |
| --- | --- | --- | --- |
| REQ-001 | Resolve launch variable templates before agent dispatch, tool plan preflight, and rework packet generation. | GPTPro F02 | Tool-critical values contain no unresolved `{Key}`, `${Key}`, or `{{Key}}` placeholders. |
| REQ-002 | Fail launch/template validation when unresolved placeholders remain in tool-critical values. | GPTPro F02 | Tests cover create-project and add-test script refs. |
| REQ-003 | Aggregate completion gate issues instead of short-circuiting at the first failure. | GPTPro F03 | Incident diagnostic includes missing helper receipt and failed solution membership readback. |
| REQ-004 | Preserve original diagnostic code, retry safety, idempotency, failed path, expected content, receipt name, and source gate metadata. | GPTPro F03 | Rework packet and parent packet cite original diagnostic facts. |
| REQ-005 | Route safe/idempotent completion-gate failures to bounded `SafeRetry` / `CurrentStepRetry`. | GPTPro F04 | First incident attempt does not become `ManagerRequired`. |
| REQ-006 | Escalate only when policy denies retry, diagnostic is unsafe/non-idempotent, budget is exhausted, or repeated fingerprint fails repair. | GPTPro F04 | Tests cover retry allowed and budget-exhausted escalation. |
| REQ-007 | Build diagnostic-specific rework packets from aggregated completion issues. | GPTPro F05 | Packet names missing receipts, resolved scripts, readback failures, and do-not-repeat scaffold guidance. |
| REQ-008 | Propagate child stopped/blocked/failed root-cause diagnostics to parent subprocess packets. | GPTPro F06 | Parent packet includes child diagnostic code and missing receipt. |
| REQ-009 | Use artifact ledger and accepted produced slots as primary subprocess artifact evidence. | GPTPro F07 | Physical file existence alone cannot satisfy a required child output. |
| REQ-010 | Stage managed artifacts before gates and accept/promote them only after completion gates pass. | GPTPro F08 | UI/projection wording distinguishes structured finalizer validity from runtime accepted completion. |
| REQ-011 | Add exact tool-plan preflight over tool name, args, paths, scopes, idempotency, and side-effect manifest. | GPTPro F09 | Preflight fails before agent execution when required script/path/manifest is invalid. |
| REQ-012 | Add typed execution classes for process steps: `AgentReasoningOnly`, `AgentWithToolPlanGuard`, `DeterministicToolPlan`, `RuntimeOwnedSubprocess`, `BranchDecision`. | GPTPro F10 | Template loader validates execution class and required typed fields. |
| REQ-013 | Guard deterministic .NET solution setup with a typed plan before introducing a runtime-owned executor. | GPTPro F01/F10 | `workspace_pwsh_run_script` is required for solution membership repair. |
| REQ-014 | Add runtime-owned .NET setup execution for scaffold/wire/readback after the guard phase proves the contract. | GPTPro F10 | Runtime can create, wire, and verify solution membership without prompt-only reliance. |
| REQ-015 | Add explicit agent capability metadata and assignment repair so deterministic tool-plan steps do not go to generic agents without needed tool capability. | GPTPro F11 | Template validation and assignment tests catch missing capability/tool contract mismatch. |
| REQ-016 | Audit and migrate all 24 process definitions, 155 step markdown files, 30 validation JSON files, 30 prompt JSON files, subprocess parent templates, Blazor templates, screenshot/writeback flows, and six artifact templates. | User request and GPTPro F11/F12 | Inventory rows are marked migrated, explicitly exempt, or blocked with source proof. |
| REQ-017 | Preserve validation strength and semantic proof. | User request | No gate is weakened to make the incident pass; negative tests reject shallow fixes. |
| REQ-018 | Keep C# boundaries maintainable and testable. | C# architecture guard | New services are cohesive, unit-testable, and do not create dependency cycles or partial-class expansion. |
| REQ-019 | Add focused unit, integration, template-validation, manual-process, and architecture-gate tests. | GPTPro test plan | Tests cover incident, placeholders, aggregation, safe retry, child bridge, artifact acceptance, templates, and manual 5032/equivalent validation. |
| REQ-020 | Keep implementation minimally invasive and compatible with existing process runtime concepts. | User AGENTS.md | Existing constants/enums/records are used or extended; magic strings and silent fallbacks are avoided. |
