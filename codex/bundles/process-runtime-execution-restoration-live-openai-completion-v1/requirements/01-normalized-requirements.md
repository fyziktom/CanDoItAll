# Normalized Requirements

| ID | Requirement | Acceptance proof |
| --- | --- | --- |
| REQ-001 | Continue from incomplete SB013-SB048 state, do not pretend prior bundle completed. | Execution report starts with current-state reconciliation and pending-scope map. |
| REQ-002 | Keep all bundle-path references out of long-lived `src` and `tests`. | `rg 'codex[/\\]bundles|process-runtime-live-e2e-openai-hardening-v1' src tests` returns no matches. |
| REQ-003 | Prove app startup and process template catalog still work. | Startup integration test and `/api/processes/templates` proof. |
| REQ-004 | Prove UI/API/project-structure launch remains green. | Large desktop Playwright + API readback proof. |
| REQ-005 | Prove persisted run lifecycle after launch. | Integration tests verifying run, step, status, work brief and project context. |
| REQ-006 | Prove dispatch/outbox/finalizer/artifacts. | Deterministic durable dispatch E2E with finalizer and artifact readback. |
| REQ-007 | Prove MAF workflow-backed and direct-agent routes. | Focused tests with fake provider and current runtime registration. |
| REQ-008 | Prove deterministic `.NET` create/modify process. | Scenario run completes and outputs governed artifact/build/test/run evidence. |
| REQ-009 | Prove generic business-analysis process. | Scenario run completes with business artifact and no software-only assumptions. |
| REQ-010 | Add guarded live OpenAI smoke. | Opt-in live test runs or explicitly skips with no false success. |
| REQ-011 | Prove scheduler/workflow-origin starts. | Starts through `ProcessesService.StartRunFromTriggerAsync`, not driver hooks. |
| REQ-012 | Prove run detail/artifact/recovery UI. | Large desktop Playwright with screenshots and API readback. |
| REQ-013 | Keep driver verification read-only. | Source scans and tests show no mutation, registry, selector, DI, manager command, scheduler/workflow hook. |
| REQ-014 | Keep Process Core generic. | Core scan finds no domain driver, `.NET`, Office, business-only, MAF, EF, storage or UI leakage. |
| REQ-015 | Produce final release-candidate closure. | Build, unit, integration, Playwright, source scans, red-team and validators pass. |
