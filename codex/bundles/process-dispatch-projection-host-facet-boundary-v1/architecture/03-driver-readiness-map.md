# Driver-readiness map

Documentation-only. Do not implement production driver APIs in this bundle.

| Future driver concept | Current internal facet that prepares it | Why this helps later |
| --- | --- | --- |
| `ArtifactProjectionEvidence` | projection orchestrator + source-family coordinators | Later drivers can produce evidence without knowing dispatcher internals. |
| `WorkspaceFileEvidence` | path/file IO/classification facets | Later SW-dev/Rust/Office drivers can reuse evidence vocabulary safely. |
| `BrowserProofEvidence` | browser output facet | Browser helper driver can remain separate from process lifecycle. |
| `ResponseTextDeliverableEvidence` | response-text facet | Business-analysis/document drivers can reuse deliverable semantics. |
| `DecisionRecordEvidence` | completed-decision facet | Governance drivers can reason about decision evidence. |
| `ProjectionLineageEvidence` | lineage facet | Future driver results can be traceable without owning process state transitions. |

Explicitly deferred: `IProcessDriverPack`, `IProcessDriverRegistry`, driver packages, driver discovery, driver security envelopes.
