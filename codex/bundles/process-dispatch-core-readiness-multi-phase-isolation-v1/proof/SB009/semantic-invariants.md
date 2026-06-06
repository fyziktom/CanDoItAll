# SB009 Semantic Invariants

- Candidate hydration loader remains read-only and side-effect free.
- Technical-agent binding side effects are explicit in `ProcessDispatchTechnicalAgentBindingCoordinator`.
- Recovery queries remain local to the dispatch boundary and do not introduce Process Core or driver contracts.
