# Normalized Requirements

| Id | Requirement | Owning Subbundles | Proof Required |
| --- | --- | --- | --- |
| RQ-001 | Remove all transient `codex/bundles/<name>` path coupling from long-lived source/tests. | SB001-SB006 | Source scan + full unit + architecture tests. |
| RQ-002 | Prove the application can start under current composition and the process module is registered. | SB007-SB009 | Web build, startup smoke, `/health`, `/api/processes/templates`. |
| RQ-003 | Prove large-screen UI process launch from global Processes route. | SB010-SB012 | Playwright large desktop, screenshot review, API assertion. |
| RQ-004 | Prove project-structure launch path preserves context. | SB013-SB015 | API/integration + large-screen project route proof. |
| RQ-005 | Prove run creation, step persistence, dispatch claim, route execution, finalizer, artifacts. | SB016-SB021 | Integration E2E over service/runtime outbox. |
| RQ-006 | Prove MAF workflow-backed and direct-agent process roles still work. | SB022-SB024 | Focused integration with deterministic provider and existing MAF services. |
| RQ-007 | Prove `.NET` app create/modify process in deterministic mode and optionally live OpenAI mode. | SB025-SB030 | Managed artifact output, build/test/run guard, optional live run with budget. |
| RQ-008 | Prove non-software business-analysis process. | SB031-SB033 | Business artifact output, generic process terms, no software-domain leakage. |
| RQ-009 | Prove scheduler and workflow-origin starts use normal process services. | SB034-SB036 | Trigger tests, scheduler launch tests, no driver hook. |
| RQ-010 | Integrate read-only driver verification as diagnostics only. | SB037-SB039 | Batch observation, manager projection, no mutation. |
| RQ-011 | Decide runtime-host/registry/selector/DI/manager-command status with source-backed roadmap. | SB040-SB042 | Architecture docs + tests rejecting accidental runtime host. |
| RQ-012 | Prove UI run detail, artifact navigation, recovery/diagnostic surfaces. | SB043-SB045 | Playwright large desktop + service readbacks. |
| RQ-013 | Establish release-candidate smoke matrix and final red-team proof. | SB046-SB048 | Build, full unit, focused integration, Playwright, optional live OpenAI, source scans. |
