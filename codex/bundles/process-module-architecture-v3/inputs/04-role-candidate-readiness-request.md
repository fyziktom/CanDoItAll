# Role Candidate Readiness Improvement Request

## Preserved User Instruction

The v3 Process architecture is missing more detailed information about selecting candidates for roles in a process.

Current implementation uses a scoring system for candidates selected by the HR agent. This must be improved so the system can judge and inform about missing tools or rights that are necessary for a role.

## Required Architecture Improvement

- Candidate selection must not be score-only.
- HR-selected candidates must be validated by deterministic readiness checks.
- A high-scoring candidate may still be not executable if required tools, rights, permissions, approvals, bindings, provider profiles, workflow availability, or project/resource access are missing.
- The UI and launch plan projections must tell the user exactly which required tools or rights are missing, whether the issue is blocking or warning-level, who or what can resolve it, and whether provisioning or approval is available.
- Launch approval and execution must be blocked when required candidate readiness blockers remain, unless a configured manager/user override policy explicitly allows the exception and records an audit decision.
