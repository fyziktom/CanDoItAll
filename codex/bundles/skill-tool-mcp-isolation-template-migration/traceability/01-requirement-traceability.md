# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Better isolation of skills/tools/MCPs from MAF | `architecture/01-target-solution.md`, `architecture/02-reconnection-map.md` | SB01, SB02, SB03, SB04, SB05, SB08, SB09 | project build, adapter unit tests, MAF integration tests, hardening proof | MAF remains adapter, not owner. |
| Own projects with abstractions and implementations | `requirements/01-normalized-requirements.md` | SB01-SB04 | compile new projects, dependency direction tests | Project names are proposed and can be adjusted during implementation. |
| Use `Templates/` for skill/tool/MCP info | `templates/01-template-pack-design.md` | SB06, SB07 | template loader tests, seed parity integration tests, no-fallback proof | Target path is `Templates/Capabilities`. |
| Internal and external tools | `templates/01-template-pack-design.md` | SB02 | internal mock call, external process/http fake call tests | External calls must be policy-gated. |
| Internal and external MCPs | `templates/01-template-pack-design.md` | SB04 | fake internal hosted and local stdio MCP list-tools tests | Explicit lifecycle ownership required. |
| Setup test for external tools and MCPs | `architecture/02-reconnection-map.md`, `architecture/03-error-and-diagnostics-model.md` | SB02, SB04, SB10 | API setup-test integration, Playwright UI flow, structured failure proof | Generic `VerifyCapabilityAsync` is not enough. |
| Structured folders in new projects | `requirements/01-normalized-requirements.md` | SB02-SB04 | project layout review and tests | Group by capability domain. |
| Tests split unit/integration/e2e | `inventories/02-test-inventory.md`, `plan/01-phase-plan.md` | All | unit, integration, component, Playwright transcripts | Workbook also tracks this split. |
| Preserve all existing behavior | `analysis/01-current-state.md`, `plan/01-phase-plan.md` | SB06, SB08, SB11 | seed parity, runtime composition, process/workflow regression | Existing keys and runtime names are compatibility contracts. |
| Naming standards | `requirements/02-naming-and-compatibility-standards.md` | SB01, SB12 | naming validation tests and docs | Runtime names remain snake_case; catalog keys remain kebab-case. |
| Prepare bundle only | `inputs/00-original-request.md`, `README.md` | Preparation | prepared-stage bundle validator | No production implementation is included. |
| Structured diagnostics and repairable errors | `architecture/03-error-and-diagnostics-model.md`, `inventories/03-error-state-inventory.md` | SB01-SB11 | negative tests, setup-test API assertions, UI proof | Blocks generic external tool/MCP failure messages. |
| Mandatory hardening/refactoring/optimization checkpoints | `architecture/04-implementation-quality-guardrails.md`, `plan/01-phase-plan.md` | SB05, SB07, SB09 | checkpoint manifests, static scans, focused performance scans | Blocks MAF reconnection and UI/API work until hardening passes. |
| Generic restrictions for skills/tools/MCPs by agent/process/workflow/UI | `architecture/05-capability-access-policy.md`, `inventories/04-capability-access-policy-test-inventory.md`, `templates/01-template-pack-design.md` | SB01-SB12 | access policy unit tests, template compatibility tests, effective-set integration tests, UI preview tests, process/workflow e2e proof | Deny/require policies restrict assigned candidates; no raw runtime selector strings or capability-kind-specific hidden filters. |
