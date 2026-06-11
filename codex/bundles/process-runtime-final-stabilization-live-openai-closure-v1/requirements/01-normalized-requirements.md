# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| REQ-001 | Re-check actual code, tests, latest release decision, and proof classification. | SB01 |
| REQ-002 | Repair release decision policy so code-first ratio is advisory unless it indicates hidden functional regression or proof-only fake closure. | SB01 |
| REQ-003 | Run the real live OpenAI process-run smoke with explicit bounded env values; do not count skip as pass. | SB02 |
| REQ-004 | If live OpenAI fails, classify provider/config/process/finalizer/artifact failure with exact fix path and rerun. | SB02 |
| REQ-005 | Rerun deterministic representative matrix: Blazor, software-delivery/multi-team, business-plan PostgreSQL automation, runtime-host readback, scheduler/workflow jobs. | SB03 |
| REQ-006 | Rerun large-screen project/project-structure launch-to-completed-run UI proof. | SB04 |
| REQ-007 | Verify runtime-host readback appears in operator-visible run detail or record exact UI blocker. | SB04 |
| REQ-008 | Keep Process Core generic and block any new execution-capable driver/selector/registry/self-registration drift. | SB05 |
| REQ-009 | Produce final stabilization decision and next-step path: merge-ready, live-provider-blocked, or runtime-blocked. | SB06 |
