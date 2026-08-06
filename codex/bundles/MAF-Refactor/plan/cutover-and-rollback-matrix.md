# Cutover and rollback matrix

| Responsibility | Expansion | Production selector | Rollback | Delete in |
|---|---|---|---|---|
| Context capture V2 | V1 adapter + V2 contracts/service | send-entry capture version | complete V1 path for new turn | SB18 writer cleanup; reader retained per policy |
| Conversation affinity | new per-chat store | new floating chat/default migration | stop updating affinity; transcript remains | no deletion; canonical target |
| Scope services | complete V2 bundle | per-execution factory version | complete legacy bundle for a new run | SB08/SB18 |
| Runtime ports | narrow adapters + broad facade | caller registration | facade delegates to adapters | SB18 |
| MAF collaborators | extracted types | DI adapter implementation | previous adapter commit, not service locator | SB11/SB18 |
| MAF dependencies | abstractions + outer composition | project references/DI | fail closed; do not restore forbidden refs | SB14/SB18 |
| Process recovery | Processes policy | policy version | disable recovery and fail | SB14 |
| Runtime state envelope | envelope writer + legacy reader | new-write schema | continue reads; no unsafe reverse overwrite | legacy reader retention decision in SB18 |
| Per-proposal approval | new command + bool adapter | UI/API contract version | exact pending-set bool adapter | SB18 adapter deletion when caller scan empty |
| Lightweight LLM | port + provider adapter | ordinary workflow executor version | select a bounded compatibility executor whose payload-scope inference is already removed, or disable the node explicitly; never restore the unsafe full-agent/payload-authority path | SB18 |

Every selector must have telemetry, an owner, a default, and a removal decision. No selector may cause dual side effects.


## Rollback safety clarifications

- A rollback target must already satisfy authority and dependency invariants. "Old implementation" is not automatically a safe rollback.
- Active runs keep the runtime adapter, context/authority reference, workspace bundle, and state schema with which they were admitted. Selectors affect only new work unless a tested migration explicitly says otherwise.
- Waiting approval runs are never forced through a different adapter/model/toolset merely to complete a rollback. They resume compatibly or surface an explicit incompatibility.
- Provider calls, tools, process completion, and persistence are never executed on both sides of a selector.
- Every selector emits bounded telemetry and has one named owner who decides retention or deletion in SB17/SB18.
