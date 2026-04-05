# Canonical truth map

| Concern | Current owner(s) | Duplicate or drift surface | Target owner | Why |
| --- | --- | --- | --- | --- |
| Node identity | ProjectObjectRecord.NodeKey + ProjectId | System-managed rows duplicate upstream nodes | Stable Workbench NodeCarrier identity for workbench-authored nodes; external projections assembled on read | Keep node as the stable carrier for brainstorming and typed evolution. |
| Node type semantics | Enum + subtype strings + UI catalog + metadata family | UI definitions and subtype branching own semantics | NodeKindRegistry | One place must declare allowed transitions, relations, actor roles, time semantics, and UI descriptors. |
| Hierarchy | ParentNodeKey and relation rows | Hierarchy stored twice; parent chain reused in dependency analysis | Single containment owner | Containment is not the same as dependency. |
| Dependencies | Generic link table + dependency analysis heuristics | Ancestors treated as prerequisites | Explicit dependency edge model | Critical path logic must be based on explicit edges/policies. |
| Spatial semantics | PositionX/PositionY on node; markers duplicated | Marker columns and marker metadata both present | Spatial semantic owner + marker owner | X/Y and semantic markers are canonical because the mindmap itself carries meaning. |
| Work-item assignee | Metadata.WorkItem.AssigneePartyId/Name and ProjectPartyAssignment rows | Two writable truths | Canonical scoped actor-assignment owner | Metadata should not be the authority for live assignments. |
| Meeting participants | Metadata.Meeting.RelatedParties and ProjectPartyAssignment rows | Two writable truths | Canonical scoped actor-assignment owner | Names can be projected, but membership should have one owner. |
| Participant directory link | Metadata.Participant.LinkedPartyId/Name and ProjectPartyAssignment rows | Identity link and assignment rows are mixed | Canonical node-to-actor link (or scoped assignment role) | A participant node should link to one actor/party through a canonical relation. |
| Resource/test/validation responsibility | Module-local ResponsiblePartyId / OwnerPartyId fields | No shared ownership matrix across modules | Explicit ownership matrix + adapters | Do not erase module-native truth blindly; stabilize ownership first. |
| Structure/calendar/Gantt | Workbench persisted synced rows | Projection acting as truth | Projection builders over assembled graph | Deleting a cache must never change canonical outcomes. |
