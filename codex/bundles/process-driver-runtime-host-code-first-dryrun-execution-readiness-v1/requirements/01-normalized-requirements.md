# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| REQ-001 | Quantify code-vs-bundle diff and make code-first ratio a hard gate. | SB001-SB003 |
| REQ-002 | Complete EF audit productionization with schema/index/query/retention proof across scopes. | SB004-SB006 |
| REQ-003 | Expose verification host status/readiness through operator-safe API/service readback. | SB007-SB009 |
| REQ-004 | Remove production dependence on sync Verify wrapper and enforce async paths for host/manager/job runner. | SB010-SB012 |
| REQ-005 | Execute scheduler/workflow-origin read-only verification jobs through typed service path without driver hooks. | SB013-SB015 |
| REQ-006 | Add manager/operator UI or API readback for host status, audit ids/hashes, denial category, and no-mutation flags. | SB016-SB018 |
| REQ-007 | Harden live process-run OpenAI smoke with explicit model/token/timeout and artifact/diagnostic readback. | SB019-SB021 |
| REQ-008 | Implement dry-run execution host contracts that deny effects by default and return structured dry-run plans. | SB022-SB024 |
| REQ-009 | Introduce sandbox/allow-list contract models with negative tests for all effectful surfaces. | SB025-SB027 |
| REQ-010 | Define domain driver pack topology and explicit non-discovery registration path without reflection fallback. | SB028-SB030 |
| REQ-011 | Preserve Process Core genericity and block domain leakage. | SB001-SB030 |
| REQ-012 | Keep bundle/proof artifacts concise and source-backed. | SB001-SB030 |
