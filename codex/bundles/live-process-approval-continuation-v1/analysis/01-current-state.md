# Current State

- Port 5032 is served by `CanDoItAll.Web.exe` from this repository and responds to `/health`.
- Run `01ee78c6-077e-4a6c-8139-1f4120e659a5` is blocked with step `a90e2f59-5033-44c1-9a91-d96ad808a610`.
- The open escalation `f67005cb-7852-4669-bf0f-cd41ea2f4e35` is `BlockedStep`, not an operator approval.
- The Live Processes card and escalation detail dialog render `Approve` and `Deny` for all escalation kinds and call `SendEscalationQuickDecisionAsync`.
- `SendEscalationQuickDecisionAsync` sends a manager-chat prompt; it does not continue an execution run, record a direct approval decision, or request rework.
- The user's click created manager-chat execution runs `57aa3d3d-7476-408a-a3c7-e2bf4c5cf50d` and `c0355121-2210-4aef-bbd5-847a067cc05e`, both waiting on `processes_run_detail_get` with no pending approval.
- Process Workspace already has correct direct operations for approval continuation and escalation rework; Live Processes should reuse those service boundaries.
