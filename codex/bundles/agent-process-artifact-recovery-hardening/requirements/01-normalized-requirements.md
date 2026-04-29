# Normalized Requirements

| Id | Requirement | Owning subbundle | Proof |
| --- | --- | --- | --- |
| REQ-001 | Diagnose the supplied real-run failure from DB state and source code, not only console excerpts. | 01 | Read-only DB query notes, source references, focused diagnosis tests if needed. |
| REQ-002 | Isolate a one-agent implementation proof before attempting whole-process validation. | 01 | Focused integration or harness test for a single implementation agent/job. |
| REQ-003 | Treat `Migration and rollout preparation checklist` as required but allow explicit no-DB/no-data-change content when valid. | 02 | Prompt/template assertions and artifact projection tests. |
| REQ-004 | Make required artifacts explicit enough that agents must write or response-project every required artifact before success. | 02 | Dispatch prompt tests and artifact satisfaction tests. |
| REQ-005 | Do not retry the wrong owner when a missing artifact belongs to an upstream step rather than the current step. | 03 | Recovery-routing tests for current-step vs upstream missing artifacts. |
| REQ-006 | Preserve strict governed completion: missing required artifacts must block/fail instead of silently completing. | 02, 03 | Negative integration tests. |
| REQ-007 | Expand mock agents to simulate repeated-write failure, missing build/test validation, missing current artifact, and missing upstream artifact. | 04 | Mock-runtime integration tests. |
| REQ-008 | Add a simpler three-agent process proof to validate artifact outputs and handoff without the full rich process. | 05 | Focused process test and, if UI-visible, Playwright proof. |
| REQ-009 | Keep whole-process rich scenario tests out of the early repair loop. | 01-05 | Execution report must show staged validation, not only a full-process run. |
| REQ-010 | A generated UI application must not be accepted when runtime startup or the primary route fails; framework-specific fixes belong in agents, skills, or tools, not process-core dispatch. | 06, 07 | Runtime/browser proof in the generated-app lane plus process-core source scans. |
| REQ-011 | Generated UI application delivery must include runtime/browser proof, not only build and domain-unit-test proof. | 06, 07 | HTTP route smoke, browser proof, screenshot review, and generic QA/implementation guidance. |
| REQ-012 | Universal process dispatch, retry, and proof code must not contain calculator, Blazor, or .NET-specific repair recipes. | 07 | Source scans and static regression tests over process dispatch files. |
| REQ-013 | Technology and task-specific guidance must live in agents, skills, or tools, while reusable seed examples stay neutral. | 07 | Seed asset scans and reviewed seed-builder/normalizer changes. |
| REQ-014 | Implementation completion proof must require concrete deliverable mutation and required validation after the latest mutation using generic tool categories. | 07 | Focused dispatch tests for required-tool resolution and validation-after-mutation proof. |
