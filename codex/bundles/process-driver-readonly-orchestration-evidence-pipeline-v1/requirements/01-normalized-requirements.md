# Normalized Requirements

| ID | Requirement | Owner subbundles | Acceptance proof |
| --- | --- | --- | --- |
| REQ-001 | Reconcile current branch and prove the latest code is source-backed after the Codex crash. | SB001-SB003 | Build/test/source scans and critical proof manifest. |
| REQ-002 | Preserve a clean full-unit baseline and prevent old skip/debt regressions. | SB004-SB006, SB046-SB048 | Full unit transcript, no unexpected skips/failures, debt ledger only if unavoidable. |
| REQ-003 | Keep Core dependency-clean and driver-free. | SB004-SB006, SB034-SB036 | Reverse dependency scan and architecture tests. |
| REQ-004 | Split broad process adapter file into narrow adapter/payload/observation/mapper files without behavior drift. | SB007-SB009 | Focused integration parity, source scans, no broad adapter file. |
| REQ-005 | Add explicit typed multi-domain batch gateway without generic runtime dispatch. | SB010-SB012 | Gateway tests reject `object`, registry, selector, DI, manager command. |
| REQ-006 | Add process read-only orchestration over supplied payloads only. | SB013-SB015 | Multi-domain process adapter integration tests and no-side-effect scans. |
| REQ-007 | Build supplied evidence payloads from already-resolved facts only. | SB016-SB018 | No File/Directory/storage/workspace reads; hash/content-type/size proof. |
| REQ-008 | Normalize observation aggregation into process read-only aggregate snapshots. | SB019-SB021 | Aggregate immutability and lane-summary tests. |
| REQ-009 | Harden audit/redaction/no-mutation across all lanes. | SB022-SB024 | Malicious corpus and no-secret leakage tests. |
| REQ-010 | Exercise artifact/Office/business lanes through the process orchestrator. | SB025-SB030 | Integration tests for each lane and denial of external/mutation operations. |
| REQ-011 | Keep public contract and Core API governance current. | SB031-SB036 | Reflected API snapshots, version history, reverse dependency tests. |
| REQ-012 | Upgrade shared verification test harness and semantic adequacy proof. | SB037-SB039 | Shared harness usage scan and production behavior artifact matrices. |
| REQ-013 | Keep runtime host explicitly not approved. | SB040-SB042 | Docs/source tests reject host/registry/selector/DI/manager/scheduler/workflow. |
| REQ-014 | Sync docs/samples with code, not prose-only. | SB043-SB045 | Source-backed sample tests. |
| REQ-015 | Close broad smoke, red-team, validators, and handoff. | SB046-SB054 | Build, full unit, focused tests, source scans, validators, zip generation. |
