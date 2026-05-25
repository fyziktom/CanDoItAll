# Current Risk Inventory

| Risk ID | Risk | Consequence | Owning subbundle |
| --- | --- | --- | --- |
| R01 | Non-mutating steps can still invoke mutating tools if prompt guard fails. | Architecture/planning/review agents can implement too early. | SB01 |
| R02 | Workflow-backed role candidate has no expected artifacts. | Workflow steps bypass process artifact contracts. | SB02 |
| R03 | Subprocess parent completion bypasses finalizer. | Subprocess artifacts are weaker than direct-agent artifacts. | SB02 |
| R04 | Subprocess placeholder record uses required expectation id. | Missing child artifact can look satisfied. | SB02/SB05 |
| R05 | Artifact mode inference is string-based. | False blocks in generic processes. | SB05 |
| R06 | Current-run lineage is loose for most producer kinds. | Stale same-step artifacts can satisfy new attempts. | SB05 |
| R07 | Downstream step is blocked before upstream rerun. | Process can stall after artifact materialization succeeds. | SB04 |
| R08 | Negative product findings become hard blocks. | Review/QA processes stop instead of routing to repair/no-go branch. | SB03 |
| R09 | Same no-progress retry can repeat. | Time/cost waste and noisy logs. | SB06 |
| R10 | Process definitions lack lint/simulation gates. | Bad step scopes and artifacts reach runtime. | SB07 |
