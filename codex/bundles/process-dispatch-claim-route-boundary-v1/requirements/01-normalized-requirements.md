# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| RQ-001 | Preserve existing MAF/Processes decoupling and process-owned execution snapshot boundary. | SB01, SB16 |
| RQ-002 | Do not introduce Process Core, driver packs, or production process-driver APIs. | SB01, SB04, SB08, SB12, SB16 |
| RQ-003 | Inventory current dispatch route, claim, heartbeat, and concurrency side effects before movement. | SB02, SB03 |
| RQ-004 | Add/extend architecture guardrails before production movement. | SB04 |
| RQ-005 | Extract execution-run selection rules from `Concurrency.cs` into module-local pure helper(s). | SB05-SB08 |
| RQ-006 | Preserve stale, blocking, recoverable, competing, and fresh-recovery behavior. | SB06-SB08 |
| RQ-007 | Introduce a module-local claim/heartbeat session boundary without changing durable claim behavior. | SB09 |
| RQ-008 | Extract start-transition and fresh-skip request/decision helpers without moving transitions. | SB10 |
| RQ-009 | Introduce a route planner for pre-execution dispatch branches without executing side effects inside the planner. | SB11 |
| RQ-010 | Preserve workflow, subprocess, agent execution, and manager recovery routing semantics. | SB11-SB13 |
| RQ-011 | Add a finalizer context factory/builder for route outcomes. | SB13 |
| RQ-012 | Add documentation-only driver-readiness map for dispatch intents and evidence families. | SB14 |
| RQ-013 | Run focused unit/integration/build proof and source scans. | SB04, SB08, SB12, SB15, SB16 |
| RQ-014 | Do not create small/medium/mobile proof artifacts. | SB01-SB16 |
