# Requirement Traceability

| Requirement | Analysis finding(s) | Architecture artifact | Owning subbundle(s) | Proof method |
|---|---|---|---|---|
| RQ-01 | F-01 through F-10 | N/A | 01 | Failing-then-passing regression corpus. |
| RQ-02 | F-01 | `architecture/01-target-solution.md` | 02 | Unit tests for low-signal keys and composite scoring. |
| RQ-03 | F-01, F-10 | `architecture/01-target-solution.md` | 02 | Persisted cluster metrics and search/display assertions. |
| RQ-04 | F-02 | `architecture/01-target-solution.md` | 03 | Dream candidate content tests and structured payload checks. |
| RQ-05 | F-03 | `architecture/01-target-solution.md` | 03 | Validator tests for overbroad/mixed/duplicate/stale cases. |
| RQ-06 | F-04 | `architecture/01-target-solution.md` | 03 | Aggregate apply tests for calibrated confidence, dedupe, and lineage. |
| RQ-07 | F-07, F-08, F-09 | `architecture/02-curator-professor-learning-model.md` | 04 | Curator capture extraction and explicit target tests. |
| RQ-08 | F-08 | `architecture/02-curator-professor-learning-model.md` | 04 | Multi-target correction regression. |
| RQ-09 | F-07 | `architecture/02-curator-professor-learning-model.md` | 05 | Assimilation lifecycle tests. |
| RQ-10 | F-07, F-08 | `architecture/02-curator-professor-learning-model.md` | 05 | Targeted dream/revalidation tests after professor correction. |
| RQ-11 | F-05 | `architecture/01-target-solution.md` | 06 | Brief quality tests and integration proof. |
| RQ-12 | F-05 | `architecture/01-target-solution.md` | 06 | Reference expansion tests through aggregate provenance. |
| RQ-13 | F-10 | `architecture/03-refactor-map.md` | 07 | Build/test/component/browser proof. |
| RQ-14 | User constraint | N/A | All | No economic-governance files or concepts modified. |
