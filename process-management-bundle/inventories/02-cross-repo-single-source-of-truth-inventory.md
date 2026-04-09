# Cross-Repo Single-Source-Of-Truth Inventory

| Concern | Canonical owner | Existing overlap | Rule for future execution |
| --- | --- | --- | --- |
| Business role templates | CRM-HR | AgentFramework runtime templates and sandbox agent definitions | Processes snapshot CRM-HR-owned templates; runtime templates cannot become business truth. |
| Durable AI identity | CRM-HR `AiAgentProfile` | AgentFramework agent definitions | Process roles bind to CRM-HR identity snapshots, then later resolve through bridge adapters. |
| Provider profiles | Workspace `ProviderProfile` | AgentFramework `ProviderProfile` | Workspace remains canonical; AgentFramework must consume or project shared provider truth. |
| Capability requirements | Processes + CRM-HR template snapshots | AgentFramework runtime capability catalog | Processes store requirements and proof snapshots; do not create a third durable registry. |
| Runtime sessions | Future external bridge with process correlation | AgentFramework chat/session documents | Every runtime session must carry process, step, and assignment correlation IDs. |
| Runtime metrics and logs | Process analytics plus external evidence links | AgentFramework metrics/log stores | Logs and metrics remain attributable to business context. |
| Project context | Projects | AgentFramework workspace documents and sandbox artifacts | Runtime may consume projected project context only. |
| Evidence payload storage | Managed artifact store now, IPFS seam later | standalone files and future IPFS node | Canonical trust metadata stays in process storage even when payload placement changes. |
| Canvas state | Processes + Canvas projections | Workbench and runtime overlays | Layout and overlays remain projections, never hidden semantic truth. |
| Approval and autonomy policy | Processes | future runtime rights and tool permissions | Runtime policy is narrowed by process step governance, not the other way around. |

## Immediate Decision

- Future execution must treat this inventory as a hard review checklist during phase 00 and again during the AgentFramework bridge phase.
