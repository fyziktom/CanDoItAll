## Phase8 refactor plan

### Stream A — seal node core vs binding/reference truth
- remove mapped binding columns from ProjectObjectRecord
- migrate legacy data into ProjectNodeBindingRecord / ProjectNodeReferenceRecord / future facet records
- stop direct mutation of binding fields on the node core
- narrow metadata envelope so foreign-owner IDs are not public writable payload

### Stream B — collapse editable-node hierarchy to one owner
- pick the canonical hierarchy owner for editable nodes
- delete hierarchy link persistence from create/reparent/move flows
- derive hierarchy links in assembly/view surfaces
- write migration + tests for old data cleanup

### Stream C — promote registry into capability owner
- extend ProjectNodeKindDescriptor/capability service
- replace workbench page and CRM/HR hardcoded role/type logic
- add tests that enforce registry-driven policy

### Stream D — finish plugin-first connector platform
- provider page: plugin-manifest driven
- resource page: plugin-manifest driven
- provider/resource save + resolve flows become plugin-key first
- legacy enum use reduced to compatibility only

### Stream E — durable connector-operation boundary
- introduce connector intent/outbox/job model
- route future side-effecting connectors through durable execution
- add idempotency / retry / approval state model

### Stream F — hotspot reduction
- split CrmHrServices.cs by responsibility
- split ProjectWorkbenchModels.cs into smaller units after the hard-gate changes land
