# SB03 architecture change record

- The neutral project now owns participant card, compact list/item, picker filtering, and their isolated CSS.
- `ConversationParticipantPresentation` uses `ConversationPresentationKey`; no Guid or Agent type crosses the boundary.
- `AgentParticipantPresentationMapper` owns Agent lifecycle/workload/private-provider/history/capability/favorite projection.
- `AgentSelectionCard`, `AgentCompactList`, `AgentCompactListItem`, and `AgentSwitchDialog` remain callable compatibility façades.
- Catalog loading, team policy, favorite persistence, notification, and dialog effects remain Agent-owned.
- Snapshot `snap-20260816112732-fa75493b` has no project cycle and preserves the dependency direction AgentFramework Components -> Conversations Components.
- The two reported module/type cycles are the same pre-existing Modules.AgentFramework cycles recorded by SB01/SB02 and do not involve the neutral project.
