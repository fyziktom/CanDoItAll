# Normalized Requirements

| ID | Requirement | Owning subbundles |
| --- | --- | --- |
| REQ-001 | Reconcile current branch from real source and tests, not report-only closure. | SB001-SB003 |
| REQ-002 | Prove EF audit persistence is the production default and in-memory is explicit test-only. | SB004-SB006 |
| REQ-003 | Add cross-scope and restart/profile audit readback proof. | SB004-SB006 |
| REQ-004 | Enforce async/cancellable host usage in production/manager/scheduler/workflow paths. | SB007-SB009 |
| REQ-005 | Convert expected host validation failures into structured denials everywhere. | SB007-SB009 |
| REQ-006 | Add host health/readiness/emergency-disable status and operator readback. | SB010-SB012 |
| REQ-007 | Harden exact lane registry/selector with options-driven enablement and no fallback/discovery. | SB013-SB015 |
| REQ-008 | Expose manager verification diagnostics via stable API and large-screen UI readback. | SB016-SB021 |
| REQ-009 | Execute scheduler/workflow read-only verification jobs through process host boundary, not driver hooks. | SB022-SB024 |
| REQ-010 | Tighten live OpenAI process-run proof: explicit model, budget, timeout, cost/usage readback. | SB025-SB027 |
| REQ-011 | Preserve deterministic process runtime safety net for .NET and business-analysis scenarios. | SB028-SB030 |
| REQ-012 | Add correlation, observability, failure taxonomy, and audit-retention readbacks. | SB031-SB036 |
| REQ-013 | Prepare dry-run sandbox/allow-list contracts for future execution-capable drivers, with zero execution. | SB037-SB042 |
| REQ-014 | Define execution-capable future approval gate and negative tests. | SB043-SB045 |
| REQ-015 | Keep Core generic and driver packages non-self-registering. | SB046-SB048 |
| REQ-016 | Run release-candidate build/unit/focused/live/UI/source-scan matrix. | SB049-SB054 |
| REQ-017 | Update docs/operator runbooks and final red-team/validator closure. | SB055-SB060 |