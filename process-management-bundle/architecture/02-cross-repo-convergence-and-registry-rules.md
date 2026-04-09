# Cross-Repo Convergence And Registry Rules

## Current Overlaps That Must Not Become Permanent

| Concern | Main repo evidence | AgentFramework evidence | Required rule |
| --- | --- | --- | --- |
| Durable AI identity | `AiAgentProfile` in CRM-HR | runtime-side agent definitions and templates | CRM-HR remains canonical for durable identity and business ownership. |
| Provider profiles | `ProviderProfile` in Workspace | `ProviderProfile` in AgentFramework.Models | Workspace remains canonical; AgentFramework must consume or project shared provider truth later. |
| Capabilities and proof state | CRM-HR currently stores capability notes; AgentFramework stores runtime capability objects and proof status | runtime-side capability catalog and proof services | Processes must not create a third registry; bundle plans a shared proof seam and snapshots only what a process run used. |
| Sessions, logs, metrics | not yet process-owned, but main repo already has activity and storage patterns | runtime-side chat sessions, execution logs, memory, metrics | future bridge must correlate everything back to process run, step, role, and assignment. |
| Project context | Projects and Workbench | sandbox workspace documents | project context stays in CanDoItAll and is linked into runtime, never copied as hidden truth. |

## Role-First Execution Rule

- A process step is authored against a role requirement and a staffing intent.
- Staffing intent may point to:
  workforce pools, suppliers, reusable role templates, AI-capable templates, or hybrid combinations.
- Executor selection happens later through staffing, assignment resolution, or external bridge logic.
- A process remains valid after assignee changes because the role requirement remains canonical.

## Migration Direction For Future AgentFramework Convergence

- Convert AgentFramework provider profiles into a consumer of Workspace provider truth.
- Convert AgentFramework agent definitions into runtime packages or execution projections, not durable business identity.
- Keep runtime-side sessions, logs, and metrics, but require process-bound correlation IDs before production use.
- Allow AgentFramework to expose capability execution mechanics while process, CRM-HR, and shared proof policy remain canonical.

## Process-Module Implementation Guardrails

- Do not add `CanDoItAll.AgentFramework.*` project references in the first process-module merge.
- Do not duplicate provider or business role tables inside `CanDoItAll.Modules.Processes`.
- Do not hide routing or collaboration inside opaque runtime prompts when the process model can represent the decision.
- Do not bind a process directly to one named agent when the real business intent is a role requirement with eligibility, fallback, and governance constraints.
