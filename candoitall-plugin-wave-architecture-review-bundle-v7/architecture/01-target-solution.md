# Target solution

## High-level direction

The next refactor should **not** turn the node into a disposable view wrapper.  
The node should remain the stable universal carrier that users manipulate on the mindmap.

## Canonical shape

### Keep canonical on the node carrier

Keep only the universally meaningful, project-graph-native fields on the carrier:

- stable node identity
- project identity
- canonical parent anchor
- active kind key
- title / subtitle / notes
- status / priority
- semantic X/Y
- canonical markers
- schedule anchors if you decide they are truly universal
- created / updated timestamps

### Move out of the carrier

Move these to typed facets/bindings:

- external artifact ownership
- media attachment details
- storage object references
- provider/resource/secret/catalog bindings
- kind-specific payload like work-item business fields, meeting fields, repository fields, environment fields, infrastructure fields

## Read assembly direction

Read-only surfaces from other modules must be assembled, not persisted into Workbench canonical tables.  
That includes project hierarchy projections, resources, prompt flows/sessions/steps, validations, and test plans.

## Node evolution direction

A node can evolve:
- from quick note
- to decision
- to work item
- to richer typed operational blocks

That evolution should keep the node identity stable, but it must write explicit transition history and facet supersession records.

## Plugin direction

Connector/plugin extensibility must use descriptor/manifest discovery.  
The architecture may still keep closed broad categories for UX grouping, but it may not require enum expansion for every new connector.
