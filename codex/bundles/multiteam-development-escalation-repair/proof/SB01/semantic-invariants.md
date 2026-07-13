# SB01 Semantic Invariants

## Invariant MTE-SB01-DIAGNOSIS

- Invariant ID: `MTE-SB01-DIAGNOSIS`
- Source raw note: Diagnose why the 5032 Calculator multiteam run escalated.
- Expected behavior: The report names root and child run ids, failing step keys, role contracts, and the specific contract/readiness gap that allowed false escalation.
- Disallowed shallow implementation: Claiming the run was merely flaky or ambiguous without citing process ids, step responsibilities, and tool/operation contracts.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt` records the original escalation finding.
- Passing test: `bundle://proof/SB01/transcripts/passing.txt` records the post-repair proof run and launch readiness check.
- Changed source files: `repo://codex/bundles/multiteam-development-escalation-repair/analysis/01-current-state.md` with hash `14077239CDA6BE3CC0658AE586C9528030A31BF400315022871DF46CC83C1596`.
- Production assertions: The current-state analysis names root run `481109e7-8b25-472d-8554-43a97a53786a`, child run `122f95e0-f6dd-418a-9d87-4b7291652b21`, and the architect implementation-approach blocker.
- Red-team negative case: A diagnosis that lacks run ids or step-contract evidence would not explain why HR/readiness accepted under-specified assignments.
- Downstream dependency check: SB02 and SB03 use this diagnosis to repair contracts and launch-readiness validation.
