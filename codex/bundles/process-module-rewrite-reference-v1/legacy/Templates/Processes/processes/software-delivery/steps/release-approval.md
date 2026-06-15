# Approve first-pass release readiness

Approve or reject release using first-pass QA proof, shipped entrypoint/runtime consistency, security posture, rollback readiness, support coverage, and the declared release boundary. Conditions must apply to the approved boundary; out-of-boundary production hardening belongs in future recommendations unless explicitly required.

## Contract
- Inputs: QA evidence that names the shipped entrypoint and referenced runtime, security outcome, Run command nodes, UI screenshot or no-UI evidence, rollback/removal plan, support ownership, and declared release boundary.
- Outputs: Approved or rejected release readiness with accountable rationale and boundary-applicable conditions only.
- Evidence: Approval note, residual risk register, rollback/removal ownership record, declared-boundary confirmation, Run command node references, Screenshots parent/image asset or no-UI evidence, and confirmation that QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts.
- Operation target scope: `ExternalProductTargetReadOnly`
