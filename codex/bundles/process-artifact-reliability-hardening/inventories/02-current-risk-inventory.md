# Current Risk Inventory

| Risk id | Risk | Severity | Evidence source | Owning subbundle |
| --- | --- | --- | --- | --- |
| R001 | Workflow-backed process roles bypass the direct-agent artifact projection/recovery path. | Critical | Dispatcher workflow handled branch and `HandleWorkflowExecutionOutcomeAsync`. | SB01 |
| R002 | Artifact completion is inferred from recorded expectation ids rather than validated artifact state. | Critical | Missing artifact resolution uses candidate recorded ids. | SB02 |
| R003 | Recovery completion depends on mutable HashSets shared through `candidate with`. | High | `DispatchCandidate` model and recovery projection check. | SB01/SB03 |
| R004 | Response text can be projected into required artifacts without mode guard. | High | `ProjectResponseTextArtifactsAsync`. | SB02/SB04 |
| R005 | Existing managed files can be projected as required artifacts, risking stale files. | Medium | `ProjectExistingManagedArtifactFilesAsync`. | SB04 |
| R006 | Missing/unreadable projection sources are often only logged. | High | Projection code logs and continues. | SB02 |
| R007 | Generic `lead`/`manager` fallback may select wrong recovery agent. | High | Manager resolver token/score functions. | SB03 |
| R008 | Placeholder/gap records may be confused with satisfying artifacts if they carry expectation ids. | High | Prior review concern; must be reverified in subprocess/projection code. | SB04 |
| R009 | Retry loop can repeat invariant artifact failures. | Critical | User-reported symptom and current recovery/retry behavior. | SB05 |
| R010 | SQLite residue could be accidentally reintroduced by old validation habits. | Medium | Branch recently removed SQLite. | SB06 |
