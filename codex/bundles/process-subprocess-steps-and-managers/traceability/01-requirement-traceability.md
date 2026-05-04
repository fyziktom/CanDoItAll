# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Use another process as a process step. | `requirements/01-normalized-requirements.md#r1` | `subbundles/01-architecture-source-of-truth-and-schema` | Component and integration tests covering subprocess step persistence. | Foundation for all later work. |
| Observe subprocess from parent process. | `architecture/01-target-solution.md#runtime-orchestration` | `subbundles/02-runtime-subprocess-orchestration` | Integration test starts child run and parent projection reports child status. | Uses query/projection, not observer threads. |
| Avoid parallelism/source-of-truth problems. | `architecture/01-target-solution.md#source-of-truth` | `subbundles/01-architecture-source-of-truth-and-schema` | Architecture revalidation after subbundle 02. | `ProcessRun` owns hierarchy. |
| Add AI manager defaults and override. | `requirements/01-normalized-requirements.md#r7` | `subbundles/03-manager-control-plane-and-hr-override` | Unit/integration tests for override and manager report. | Store agent id and name snapshot. |
| HR matching honors manager override. | `requirements/01-normalized-requirements.md#r8` | `subbundles/03-manager-control-plane-and-hr-override` | Targeted staffing test. | No string-only matching. |
| Manager reports and instructions. | `requirements/01-normalized-requirements.md#r9` | `subbundles/03-manager-control-plane-and-hr-override` | Runtime projection test and journal/control-plane test. | Instructions must be explicit. |
| Add/change subprocess in canvas/UI. | `requirements/01-normalized-requirements.md#r10` | `subbundles/04-canvas-and-editor-ui` | bUnit/component test plus browser proof. | Right-click and editor paths both covered. |
| Double-click subprocess opens new tab. | `requirements/01-normalized-requirements.md#r11` | `subbundles/04-canvas-and-editor-ui` | Browser/JS interop proof or component test. | Needs route/query contract. |
| Distinct subprocess visual style. | `requirements/01-normalized-requirements.md#r12` | `subbundles/04-canvas-and-editor-ui` | Canvas surface tests and screenshot review. | Own family/palette/icon. |
| Add default .NET subprocess templates. | `requirements/01-normalized-requirements.md#r13` | `subbundles/05-default-software-development-subprocess-templates-and-agents` | Template pack loader tests and template import test. | Parent template references child process key. |
| Analyze Agent Framework 1.3. | `analysis/01-current-state.md#agent-framework-13-analysis` | `subbundles/01-architecture-source-of-truth-and-schema` | Architecture notes and source refs. | Use concepts, not persisted SDK types. |
| Real validation on small cases. | `requirements/01-normalized-requirements.md#r14` | `subbundles/06-validation-real-world-scenarios` | Execution report commands and scenario notes. | PostgreSQL scenario when available. |
