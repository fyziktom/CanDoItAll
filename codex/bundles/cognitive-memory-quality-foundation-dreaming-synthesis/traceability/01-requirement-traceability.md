# Requirement Traceability

| Requirement | Subbundle(s) | Proof Required |
|---|---|---|
| R-01 | 01 | Audit report, quality metrics, tests showing current shallow behavior. |
| R-02 | 02 | Cluster key computation tests across key families. |
| R-03 | 02, 03 | EF persistence tests for cluster/run/member records. |
| R-04 | 03 | Separate APIs/tests for incremental consolidation vs dream consolidation. |
| R-05 | 03 | Mode-specific tests for at least ProjectNightly, ProcedureMining, FailureLearning, and KnowledgeCoverageRefresh. |
| R-06 | 03, 04 | Aggregate candidates created from cluster members. |
| R-07 | 04 | Claim-source map records for each aggregate statement. |
| R-08 | 05 | Validation tests for evidence coverage, contradiction, redaction, temporal/stability state. |
| R-09 | 05 | Review item tests for weak/high-risk aggregates. |
| R-10 | 06 | Recall tests proving SideContext is not promoted to Selected. |
| R-11 | 06 | Synthesis service tests producing concise memory briefs. |
| R-12 | 06 | Reference resolver tests for every synthesized statement. |
| R-13 | 06 | Agent package tests proving no default score/reference flood. |
| R-14 | 04, 05, 06, 07 | Redaction/access tests for aggregate text, synthesized brief, and reference resolver. |
| R-15 | 01, 03, 07 | Dream run quality report and metrics assertions. |
| R-16 | 07 | Regression corpus with duplicates, contradictions, temporal updates, project boundaries, restricted content. |
| R-17 | All | Existing tests remain green or are deliberately updated with equivalent proof. |
| R-18 | All | Scope review confirms no economic memory control introduced. |
| R-19 | 07 | Updated docs and execution report. |
