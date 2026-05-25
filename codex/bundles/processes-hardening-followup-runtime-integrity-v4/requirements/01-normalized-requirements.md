# Normalized Requirements

| Requirement | Description | Owning subbundle |
| --- | --- | --- |
| RQ01 | Make upstream artifact materialization reactivation transaction-safe and ensure the just-recorded artifact can unblock dependent steps. | SB01 |
| RQ02 | Replace long lineage encoded in `ExternalReferenceKey` with typed provenance/hash metadata. | SB02 |
| RQ03 | Prevent non-mutating process steps from mutating product targets through script/run tools or hidden helper side effects. | SB03 |
| RQ04 | Replace free-text external target grounding with typed trusted grounding sources and explicit alias promotion rules. | SB04 |
| RQ05 | Validate artifact content through storage-backed reads, not only path extension or inline summaries. | SB05 |
| RQ06 | Add explicit workflow and subprocess artifact output mappings instead of kind/title heuristics. | SB06 |
| RQ07 | Prevent negative branch routing from masking failure to produce the current step's own required artifacts. | SB07 |
| RQ08 | Add persisted typed step operation contract fields, editor support, and import/export compatibility. | SB08 |
| RQ09 | Persist no-progress retry fingerprints and reconcile active/stale execution runs across restarts. | SB09 |
| RQ10 | Strengthen lint gates and red-team validation for high-criticality/autonomous process definitions. | SB10 |
