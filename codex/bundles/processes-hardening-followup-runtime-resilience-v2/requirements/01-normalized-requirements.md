# Normalized Requirements

| ID | Requirement | Owning subbundle |
| --- | --- | --- |
| RQ01 | Add an explicit step operation contract that separates managed process artifact creation from product/target mutation. | SB01 |
| RQ02 | Ensure analysis, architecture, scope, planning, review, and approval boundaries cannot mutate product targets unless explicitly allowed. | SB01, SB02 |
| RQ03 | Make tool policy enforce process boundaries for external targets and managed output product paths, not only prompt text. | SB02 |
| RQ04 | Prevent prompt-grounded aliases from auto-promoting read-only process targets into writable targets. | SB02 |
| RQ05 | Fix manager recovery lineage so recovered artifacts validate against recovery execution id and recovered-for id. | SB03 |
| RQ06 | Add workflow output to process artifact projection before finalizer validation. | SB04 |
| RQ07 | Add subprocess projection source-run versioning and stale parent artifact prevention. | SB04 |
| RQ08 | Implement upstream artifact materialization resolved/unblock lifecycle. | SB05 |
| RQ09 | Route artifact validation failures to branch outcomes only on appropriate disposition steps. | SB06 |
| RQ10 | Make artifact validation storage-backed and explicit-mode friendly. | SB07 |
| RQ11 | Strengthen no-progress retry compression across repeated invalid artifacts and stable failure fingerprints. | SB08 |
| RQ12 | Do not finalize active non-terminal concurrent executions. | SB08 |
| RQ13 | Integrate process definition lint into publish/start/readiness gates. | SB09 |
| RQ14 | Add generic red-team validation covering software and non-software process types. | SB10 |
