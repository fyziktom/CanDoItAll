# Normalized Requirements

| Id | Requirement | Observable success |
| --- | --- | --- |
| `R001` | Reduce responsibility owned by `ProjectStructurePage`. | Duplicated process context/output-root logic and hierarchy selection rules no longer live in page partials. |
| `R002` | Establish one owner for process launch context policy. | Page and agent service call the same top-level builder; duplicate implementations are deleted. |
| `R003` | Preserve process context behavior. | Tests cover ordering, row/asset limits, selected-node inclusion, visual-target inclusion, generated-evidence exclusion, and path redaction. |
| `R004` | Preserve output-root behavior. | Tests cover direct metadata, ancestor fallback, precedence, malformed metadata, and all existing launch-variable aliases. |
| `R005` | Isolate hierarchy candidate/cycle policy. | Page delegates to a top-level policy with no UI state dependency. |
| `R006` | Preserve hierarchy safety. | Tests reject self, direct duplicates, ancestors, descendants, and current parent while accepting unrelated candidates. |
| `R007` | Prevent shallow modularity. | No new partial/nested boundary/interface; architecture source test proves old methods are absent and both production callers use the new owner. |
| `R008` | Preserve all unrelated Project Structure behavior. | Affected Workbench build and targeted Unit/Component regression suites pass; integration limitation is explicit if environment-bound. |
| `R009` | Close durable bundle state honestly. | Every raw note maps to requirements, proof, and closure; architecture and completed bundle gates agree. |
