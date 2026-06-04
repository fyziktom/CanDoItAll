# Structured Input

## Raw Notes

| Raw note | Exact wording | Requirement | Owner |
| --- | --- | --- | --- |
| RN01 | Audit latest successful run evidence, recent local bundle reports, and remaining proof debt. | RQ01 | SB01 |
| RN02 | Map the current Processes runtime architecture and define service boundaries. | RQ02 | SB02, SB16 |
| RN03 | Consolidate artifact validation, read-model status, health, recovery, and UI/API projection semantics. | RQ03 | SB03, SB04, SB13 |
| RN04 | Harden artifact storage, lineage, dedupe, content hash, retention, and stale artifact handling. | RQ04 | SB04 |
| RN05 | Refactor output grounding/final-delivery contract logic into a testable generic service. | RQ05 | SB05 |
| RN06 | Harden project-structure run folder projection and avoid noisy artifact subtree nodes. | RQ06 | SB06 |
| RN07 | Harden manager chat resolution and run inspection capabilities. | RQ07 | SB07 |
| RN08 | Close MAF 1.6 runtime proof debt and tool/skill regression gaps. | RQ08 | SB08 |
| RN09 | Update template pack and live-run profile governance after real-process learning. | RQ09 | SB09 |
| RN10 | Build an agent skill/tool matrix that prevents agent improvisation. | RQ10 | SB10 |
| RN11 | Keep API/OpenAPI/process tools aligned with runtime models. | RQ11 | SB11 |
| RN12 | Refresh documentation and Codex skills. | RQ12 | SB12, SB17 |
| RN13 | Improve operator observability/debuggability. | RQ13 | SB13 |
| RN14 | Protect generic non-software and agent-training process scenarios. | RQ14 | SB14 |
| RN15 | Refactor timeout-prone proof/test harnesses into maintainable test taxonomy. | RQ15 | SB15, SB18 |

## Hard Boundaries

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths into production code.
- Keep Processes above Workflows.
- Keep process runtime generic across software, business, incident response, governance, and agent-training processes.
- Do not weaken artifact validation.
- Do not satisfy runtime proof requirements with docs-only changes.
- Keep PostgreSQL-only runtime assumptions.
