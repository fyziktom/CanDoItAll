# Write Coordinator Boundary

The write coordinator is a local Processes module helper, not a core service.

It may own:

- `StoragePlacementRequest` construction
- storage placement call
- `ProcessArtifactRecordRequest` construction
- invocation of the existing artifact recording path
- success/failure result normalization

It must not own:

- source discovery
- expectation matching
- dispatch claim ownership
- step transitions
- retry decisions
- browser proof decisions

The first production migration must use it only for execution-artifact projection. Other source paths remain orchestrated by dispatcher until follow-up proof exists.
