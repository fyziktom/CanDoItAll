# Node evolution and actor-truth map

| Scenario | Current state | Target canonical model | History handling | Preservation rules |
| --- | --- | --- | --- | --- |
| Simple brainstorm note | Node of type Note with text only | Workbench-native NodeCarrier with note/simple kind | None or NoteFacet | Keep stable NodeId/NodeKey, title/body, XY, markers, parent, explicit edges |
| Promote brainstorm to task | UI only supports note→ProjectBlock | Registry-driven transition Note → WorkItem.Task | Create active WorkItemFacet, retire NoteFacet if needed, record NodeTransitionHistory | Preserve spatial semantics; preserve links; optionally retain original note body as history snapshot |
| Promote brainstorm to decision | Not supported | Registry-driven transition Note → Decision | Create DecisionFacet + transition record | Preserve same node identity so the thought trajectory stays analyzable |
| Convert task to decision or back | Not modeled | Allowed/disallowed by NodeKindRegistry | Retire/create facets, never destructive delete/recreate by default | Actor assignments are preserved, remapped, or dropped by explicit transition policy |
| Bind participant node to real person/agent/partner | Metadata + assignment duplication | Canonical node-to-actor link or scoped assignment role | Represent stable identity link without duplicating live truth in metadata | Display names become projection data |
| Meeting participant edits | Metadata.RelatedParties + assignment rows | Canonical multi-actor link set on meeting scope | Meeting facet reads projected participant names | Meeting node stays stable; links change independently |
| Cross-module external projection node | Persisted workbench system-managed row | Assembled external projection node | No reclassification by default; create derived workbench follow-up node if needed | Protect external canonical aggregates from accidental mutation through graph editing |
