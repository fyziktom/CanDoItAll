# 03 — UI Composition And Flow Design

## Shell-Level Additions

### Main navigation

Add two top-level entries:
- `Collaboration`
- `Agents` (or `AI / Agents`, podle naming convention v shellu)

### Main layout

Use existing CanDoItAll shell:
- keep current menu and top bar,
- add unread badge / indicator for Collaboration,
- optionally add a small approval count badge for Agents/Processes if needed.

## Collaboration UX

### Primary route

- `/collaboration`

### Internal tabs

- `Inbox`
- `Threads`
- `Escalations`
- `Audit / History` (optional admin view)

### Core UI expectations

- Notification list with severity, unread state, timestamp, route link.
- Conversation detail showing participants, role labels, process context and full message transcript.
- Quick filters for `Requires my response`, `Process run`, `Staffing`, `Approval`, `Escalation`.
- Deep links back to process run, launch plan, CRM-HR resource or agent detail.

## Agents UX

### Primary route

- `/agents`

### Internal tabs mapping the sandbox intent

| Integrated tab | Origin in sandbox | Purpose after integration |
| --- | --- | --- |
| `Overview` | `Home` + `IntegrationMap` | Health summary, counts, latest approvals, recent runs, module status. |
| `Agents` | `Agents` | Technical agent definitions, templates, bindings, lifecycle. |
| `Providers` | `Providers` | Rehosted Workspace-backed provider management under the agent shell. |
| `Chat` | `Chat` | Operator chat and run detail surface using integrated persistence. |
| `Capabilities` | `Capabilities` | Capability proofs, tool permissions and policy summaries. |
| `Governance` | `Memory` + approval views | Pending approvals, checkpoints, run recoverability, policy warnings. |
| `Scenarios` | `ScenarioHarness` | Integrated scenario execution and proof surface. |
| `Diagnostics` | `Hosting` / low-level status | Admin-only diagnostics for background workers, provider bridge and workspace scope. |

### Tabs intentionally not copied 1:1

- Sandbox shell navigation itself.
- Any sandbox-only hosting bootstrap page.
- Any page that assumes a stand-alone workspace root chooser or demo-only host control.

## CRM-HR UX Changes

### `/crm-hr/agents`

Keep this page as business-facing resource lens:
- directory party,
- stewardship,
- validation status,
- business capability tags,
- project usage / availability,
- binding status to technical agent definition.

Add:
- `Open technical definition` deep link to `/agents?tab=Agents&partyId=...`
- inline read-only technical summary pulled from AgentFramework facade
- controlled edit actions that call AgentFramework service for technical fields

Do not keep:
- direct canonical editing of provider/runtime fields inside CRM-HR persistence.

## Processes UX Changes

### Process designer

Add new canvas link type:
- `Messaging`

Visual expectations:
- role-to-role only,
- visually distinct from responsibility and decision links,
- context menu / inspector to edit direction and notes if needed.

### Process launch

Add explicit launch/staffing step before the run starts:
- role candidate matrix,
- proposed new-agent creation tiles,
- manager/human approval summary,
- readiness gate status.

### Run detail

Add sections:
- assigned resources with provenance,
- conversation transcript for this run,
- pending escalations,
- agent execution summary,
- artifacts promoted to canonical evidence.

## UX Validation Questions

Every UI-affecting subbundle must answer these questions with screenshots and notes:

- Je hlavní cíl obrazovky čitelný bez znalosti sandbox hostu?
- Je jasné, která data jsou business-facing a která technical?
- Je role-based staffing flow průchodný bez duplicitních formulářů?
- Je vidět unread/attention state a dá se z něj dostat na konkrétní run?
- Je procesní Messaging link na canvasu rozpoznatelný a nezaměnitelný s jinými linky?
- Neztrácí se při deep-linku context mezi CRM-HR, Processes a Agents?
- Je layout použitelný na desktopu i na užším viewportu, pokud subbundle mění rozložení?

## UI Coverage Gate

Žádná user story z workbooku nesmí zůstat bez konkrétního UI surface nebo explicitního “admin-only / system-only” vysvětlení. Pokud executor zjistí novou mezeru, musí ji doplnit dřív, než uzavře final validation.
