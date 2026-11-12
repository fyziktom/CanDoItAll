# Mutation Inventory

Generated from the adjacent JSON register. Narrative documents may also require review after decisions change.

| id | owner | operation | evidence_level | proposed_boundary | phase |
| --- | --- | --- | --- | --- | --- |
| MUT-01 | Projects | Definition update/replace | selected-code | Project scalar + phases + options content stamp; current lifetime/source checks remain | P02 |
| MUT-02 | Projects | Create/reserved create/subproject create | selected-code-and-guidance | Stable operation identity + creation admission + exact accepted lifetime/result | P02 |
| MUT-03 | Projects | Phase-only and option-only writes | discovery-required | Same initial edit stamp advanced atomically; child identity validation | P02 |
| MUT-04 | Projects | Hierarchy attach/remove/reconnect | selected-code | Exact lifetimes + graph invariants + existing global hierarchy/project gates | P05 |
| MUT-05 | Projects | Delete/recreate/compensate creation | documented-contract | Lifetime retirement + effect proof + guarded compensation; receipt history retention | P02 |
| MUT-06 | Projects | Import/export/transfer/restore | documented-contract | Fresh local lifetime/stamp/namespace; imported records history only | P02 |
| MUT-07 | Search | Project search projection/rebuild | selected-code | Source/lifetime ordering + atomic participation or durable rebuild intention | P02 |
| MUT-08 | Workbench | Agent project save adapter | selected-code | Original target stamp, trusted source authority and operation result preserved | P02 |
| MUT-09 | Workbench | Canonical task details/assignment/schedule | selected-code | Preserve expected/proposed state, assignment revision and admission; add only identified gaps | P05 |
| MUT-10 | Workbench | Nodes/notes/placements/links/graph changes | documented-contract | Owner-specific content or graph/set witnesses; distinguish layout from task truth | P05 |
| MUT-11 | Workbench planning | Governed planning publication | proposed-boundary | Target CAS + bounded mandatory basis + membership + current attempt | P04 |
| MUT-12 | CRM/HR | Parties/opportunities/staffing/capacity | documented-contract | Preserve existing token/state/privacy semantics; scope capacity/assignment invariants | P05 |
| MUT-13 | Agents/Providers | Technical definition/provider configuration | documented-contract | Existing editor/version/outcome behavior; runtime configuration generation distinct | P05 |
| MUT-14 | Simple Chats | Definition edit and conversation append | documented-contract | Keep definition version and durable dispatch/event identities; no whole-chat token | P05 |
| MUT-15 | Prompts | Prompt definition/version/assets | documented-contract | Distinguish mutable draft from immutable version and asset lifecycle | P05 |
| MUT-16 | Collaboration | Thread create/reply/flags/status | documented-contract | Append reply dedupe separate from conditional status/flags; UI gate not owner concurrency | P05 |
| MUT-17 | Resources | Resource catalog metadata | documented-contract | Metadata stamp distinct from content bytes and stable placement | P05 |
| MUT-18 | Storage | Byte replacement/placement/recovery | documented-contract | Existing intents/Uncertain state plus driver-specific conditional bytes | P06 |
| MUT-19 | Workspace | Settings/data sources/storage configuration | discovery-required | Owner-defined config stamp and active profile lifecycle; avoid global scope | P05 |
| MUT-20 | Memory | Provider settings/operations | discovery-required | Configuration edits separate from append/retention/background effects | P05 |
| MUT-21 | Plugins/Security | Grants/policy/OAuth/settings/revocation | discovery-required | Current authority checked at effect; CAS does not replace capability/security enforcement | P05 |
| MUT-22 | Scheduler Planner | Plan edit and launch admission | documented-contract | Plan edit version distinct from fire identity/launch definition; misfire repair independent | P07 |
| MUT-23 | Workflows/Processes | Definitions/prepared launches/runs/checkpoints/approval/recovery | documented-contract | Separate immutable definitions, target condition, basis, approval and active attempt | P07 |
| MUT-24 | TestLab/maintenance | Scenario setup/test fixtures/seed and recovery tools | discovery-required | Explicit controlled setup through owners/maintenance; protect production paths | P05 |
| MUT-25 | Activity/audit | Optional feed append versus durable audit | selected-callsite-only | Optional feed cannot be presented as guaranteed effect audit; mandatory audit atomic/durable | P02 |
