# Root And Child Run Index

## Root Run

| Field | Value |
| --- | --- |
| Run id | c4888f4f-eabd-469f-80a6-3fccf6018a12 |
| Status | NeedsAttention |
| Current step | qa-validation |
| Current step instance | 1ebeadbe-98c9-4e9d-af3b-1e9f69a75c62 |
| First event | 2026-07-09T05:09:50.686655-04:00 |
| Last event | 2026-07-09T05:25:31.05606-04:00 |
| Event count | 62 |
| Diagnostics count | 7 |
| Result lineage count | 8 |

Raw files:
- `api/target-run.json`
- `api/target-history.json`

## Child Runs

| Child run id | Status | Time window | Agent steps |
| --- | --- | --- | --- |
| 8476b73f-6ab3-4e0b-be97-dc501dbfc71e | Completed | 05:11:30 to 05:13:30 | architecture-handoff, classify-dotnet-application, draft-architecture-design, review-architecture-design |
| 4061331e-1a71-421b-807a-a01ce08c60c3 | Completed | 05:14:25 to 05:16:02 | scaffold-contract, setup-handoff, validate-first-build |
| fef178ba-8721-4550-aab5-f971523957cd | Completed | 05:16:06 to 05:21:06 | code-change, feature-handoff, feature-slice-intake, implementation-approach, targeted-validation x3, test-contract |
| 1fb9c330-85c7-419a-9a55-6decb509fe4b | Completed | 05:13:34 to 05:21:55 | add-tests-and-proof, slice-architecture-check, slice-handoff, slice-intake |

Raw files:
- `api/child-runs-summary.json`
- `api/child-runs/<run-id>/run.json`
- `api/child-runs/<run-id>/history.json`
- `api/child-runs/<run-id>/agent-execution-runs-list.json`
- `api/child-runs/<run-id>/agent-runs/*`

Interpretation note: root process lineage reports architecture-review and implementation as completed. The detailed agent work for those came through child runs, not only root-run agent executions.
