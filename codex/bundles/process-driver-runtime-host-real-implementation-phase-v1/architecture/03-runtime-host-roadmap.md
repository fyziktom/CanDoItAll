# Runtime Host Roadmap

## This Bundle Approves
- Stable contracts for verification and dry-run runtime-host behavior.
- A process-module-owned host pipeline for read-only verification and dry-run planning.
- Static capability descriptors and explicit capability-provider boundaries.
- Durable audit/readback for host decisions.
- Scheduler/workflow read-only verification jobs routed through process-owned services.

## This Bundle Does Not Approve
- Execution-capable drivers.
- Shell command execution.
- Package restore.
- Workspace/storage writes.
- Office/Graph calls.
- CRM mutation.
- Transition/finalizer/claim/retry/process mutation.
- Reflection discovery.
- Fallback selector.
- Driver self-registration.
- Generic host that can execute arbitrary `object` payloads.

## Future Approval Bundle Must Prove
- lifecycle owner,
- cancellation/timeout/failure handoff,
- immutable audit persistence,
- sandbox and allowlist policy,
- authorization/approval/revocation/emergency-stop,
- compatibility/versioning,
- malicious corpus,
- red-team proof,
- operator observability,
- rollback plan.
