# 01 — Scope Inventory

## Affected CanDoItAll Modules

| Module | Today | Integration impact | Target role after migration |
| --- | --- | --- | --- |
| `Workspace` | Provider master data, settings, project structure agent admin, legacy provider execution | High | Retain provider master data + secrets, drop canonical AI runtime behavior. |
| `Security` | Secret storage and protection | Medium | Continue as canonical credential owner for provider secrets. |
| `CRM-HR` | Parties, staffing, project assignments, AI agent business profile | High | Remain canonical resource pool and business owner of AI resource identities. |
| `Processes` | Role model, runtime, outbox, canvas | Very high | Add launch planning, messaging policy and agent orchestration boundaries. |
| `Automation` | Durable message transport | High | Reuse as transport/outbox backbone, not user-facing conversation store. |
| `Activity` | Audit/activity stream | Medium | Receive projections from Collaboration, AgentFramework and Processes. |
| `Web/Composition` | Shell, menu, layout | High | Add Collaboration + Agents module entries and badges. |
| `TestLab` | Validation infrastructure | Medium | Host integrated scenarios and admin-only diagnostics. |

## Affected AgentFramework Source Areas

| Source area | Why it matters | Integration stance |
| --- | --- | --- |
| `Models` | Core provider/agent/chat/execution shapes | Copy and adapt into module-local domain models. |
| `Core` | Runtime seams and application services | Copy and adapt. |
| `Maf` | Execution engine | Copy and adapt. |
| `Persistence` | Workspace stores and file persistence | Copy selectively; replace sandbox assumptions. |
| `Hosting` | DI composition root | Copy and heavily adapt to real integrated composition. |
| `Components` | Reusable UI bits | Copy and adapt to CanDoItAll component ecosystem. |
| `Sandbox` | Pages, scenario harness, shell bootstrap | Recompose pages and harness logic; drop sandbox host shell. |

## Out-Of-Scope For Initial Merge

- Multi-tenant separation beyond project/process scoping.
- Provider-native capabilities not supported by the imported runtime or the current CanDoItAll governance model.
- A brand-new generic chat/collaboration system for all business modules outside the scope required by agent/process integration.
- Rewriting CRM-HR staffing fundamentals unrelated to process launch orchestration.
- Replacing the whole Activity module with Collaboration; Activity remains a projection sink.
