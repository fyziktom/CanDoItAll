# CRM AI-Agent Directory Gap And Repair

## What Was Not Clear

- The original backward-plan run created B04 AI-agent participant nodes and task ownership only inside project structure.
- CRM / HR Agents remained empty because no matching `PartyType.AiAgent` directory records or `CrmHr_AiAgentProfiles` were ever created.
- This made the earlier closure note too optimistic: the plan looked correct on the canvas, but the CRM module could not see or reuse those agents.

## Why It Matters

- Local-only participant nodes are not enough when the product explicitly treats AI agents as first-class CRM / HR parties.
- Without canonical CRM agent records, the plan cannot round-trip cleanly into the CRM module, and work-item ownership stays partly cosmetic.
- Future readers could believe the AI lane was operational even though the CRM directory had zero agent profiles.

## Repair Applied

- Created real CRM AI-agent parties and AI-agent profiles for `CRM Domain Steward`, `Relationship Mapper`, and `Follow-up Guardian`.
- Added capability packs, summaries, default model, execution mode, and review status so the CRM roster is meaningful instead of placeholder-only.
- Bound the B04 participant nodes through canonical `CrmHr_ProjectPartyAssignments` with assignment kind `AiAgent`.
- Bound the three B04 work items through canonical `WorkItemAssignee` assignments to the same CRM AI-agent parties.
- Updated the bundle artifacts so the created-plan summary now records the CRM directory bindings instead of only the local participant nodes.

## Follow-Up

- Future backward-plan runs should always include the CRM AI-agent repair step, or call the backfill script with the SQLite path so the companion repair runs automatically.
