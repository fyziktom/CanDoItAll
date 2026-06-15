# Perform security review after repair

Review trust-boundary, dependency, secrets, and data-handling impact after the repaired QA path is accepted. Scale findings to the declared release boundary instead of inventing production controls that are outside the approved handoff.

## Contract
- Inputs: QA-accepted repaired package, changed-surface inventory, repair notes, and data-handling notes.
- Outputs: Security outcome with explicit approval, block, or exception rationale tied to the declared release boundary.
- Evidence: Security review notes, exception rationale, boundary-applicable controls, and future production controls when they are outside the current boundary.
- Operation target scope: `ExternalProductTargetReadOnly`
