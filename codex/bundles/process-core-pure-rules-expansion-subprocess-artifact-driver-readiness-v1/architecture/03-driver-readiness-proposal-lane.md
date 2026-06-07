# Driver Readiness Proposal Lane

## Decision
No production process-helper-driver API is introduced in this bundle.

## Proposal-only lanes
- Route decision verifier.
- Subprocess lifecycle verifier.
- Artifact expectation/matching verifier.
- Runtime evidence verifier.
- Domain-specific helper concepts for .NET, Rust, Office, and business analysis.

## Hard ban for this bundle
Do not add:
- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- driver DI registration
- manager tools
- runtime driver selectors
- execution-capable helper tools
- driver package projects

## Future prerequisite
A later bundle may propose driver contracts only after the Core pure-rule boundaries remain green for at least one cycle and permission modes are approved explicitly.
