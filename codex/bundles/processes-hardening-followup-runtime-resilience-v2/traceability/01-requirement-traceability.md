# Requirement Traceability

| Requirement | Subbundle | Proof |
| --- | --- | --- |
| RQ01 | SB01 | Operation contract tests and source assertions |
| RQ02 | SB01, SB02 | Architecture step cannot mutate product; artifact-only write still allowed |
| RQ03 | SB02 | Tool policy denies external and managed product mutation |
| RQ04 | SB02 | Prompt alias auto-promotion red-team |
| RQ05 | SB03 | Recovery artifact validates against recovery lineage |
| RQ06 | SB04 | Workflow-backed process artifact projection |
| RQ07 | SB04 | Subprocess parent projection source-run versioning |
| RQ08 | SB05 | Blocked downstream step unblocks after upstream artifact appears |
| RQ09 | SB06 | Review routes to repair branch; artifact-production step blocks |
| RQ10 | SB07 | Malformed relative JSON fails; valid relative JSON passes |
| RQ11 | SB08 | Repeated invalid artifact attempts compress |
| RQ12 | SB08 | Active running execution is not finalized |
| RQ13 | SB09 | Linter integrated into publish/start/readiness |
| RQ14 | SB10 | Generic red-team suite |
