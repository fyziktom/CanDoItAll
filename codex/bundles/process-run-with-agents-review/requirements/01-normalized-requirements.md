# Normalized Requirements

- REQ-001: A user can launch an agent-backed process from UI through launch planning, approval, provisioning, and execute-ready actions.
- REQ-002: A user can observe active, retrying, blocked, failed, completed, and waiting-approval process state from UI without reading logs.
- REQ-003: The UI exposes AgentFramework execution attempts, raw execution status, governed process status, retry count, recovery classification, and last actionable reason.
- REQ-004: The UI exposes outbox status for automation dispatch records, including pending, leased, next retry, attempts, last error, and dead-letter state.
- REQ-005: Each step exposes an artifact obligation ledger showing required expectations, produced AgentFramework artifacts, projected process artifact records, auto-projected response artifacts, missing artifacts, and projection failures.
- REQ-006: If an agent does not deliver a required artifact, the process must move to a predictable state and show the missing artifact names, why they are missing, and what action is available.
- REQ-007: If an agent crashes, is interrupted, or loses context, the system must create or expose a structured recovery context for rerunning the work with proper instructions.
- REQ-008: Operators can manually retry or rerun a failed/blocked agent-owned step with a controlled recovery directive, without silently completing the step.
- REQ-009: Dead-lettered or repeatedly failing automation creates a process health signal visible from the run workspace.
- REQ-010: Browser E2E proof must cover launch, active observation, execution details, artifacts, and at least one negative recovery path.
- REQ-011: The implementation must not weaken strict governed completion, branch outcome selection, required artifact matching, required tool proof, or approval gating.
- REQ-012: All new UI surfaces must use existing Process Workspace components and BaseLib patterns already present in the module.
