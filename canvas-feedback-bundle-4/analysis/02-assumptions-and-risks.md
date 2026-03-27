# Assumptions And Risks

## Assumptions

- Reusing the shared canvas create composer for edit flows is preferable to inventing a second page-local modal.
- The feedback about editing "node settings" is satisfied by editing the canonical node content fields and typed metadata fields, while the existing quick controls continue to handle status, progress, marker, and priority.
- The advanced accordion can safely absorb Artifact, Kind, Location, and the existing typed fact rows without harming discoverability.

## Risks

- The edit flow touches shared canvas contracts, page orchestration, and persistence together, so an incomplete type map will create silent gaps if not tested explicitly.
- Some node types use `startUtc` and `endUtc`, so the new edit persistence path must not regress schedule handling.
- The node-action restyle is UI-heavy; without a live browser pass there is residual risk around wrapping and button density in the floating window.

## Required Guardrails

- keep metadata mapping strongly typed and reuse existing catalog definitions instead of introducing ad hoc field dictionaries
- keep failure explicit when a node cannot produce an edit model
- preserve current preview, transcript, local-open, and runtime-launch behaviors while restyling the inspector
