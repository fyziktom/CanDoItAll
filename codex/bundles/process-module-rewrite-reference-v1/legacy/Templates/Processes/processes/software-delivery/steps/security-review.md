# Perform security and data-handling review

Review sensitive-data handling, secrets, boundary changes, and policy exceptions only after first-pass QA accepts the quality evidence. Scale findings to the declared release boundary instead of inventing production controls that are outside the approved handoff.

## Contract
- Inputs: QA-accepted package, changed-surface inventory, and data-handling notes.
- Outputs: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.
- Operation target scope: `ExternalProductTargetReadOnly`
