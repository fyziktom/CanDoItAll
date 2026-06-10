# Assumptions, Risks, and Reopen Triggers

## Assumptions
- `maf-processes-refactor` remains the active branch.
- PostgreSQL test availability may vary; EF audit proof must have a deterministic test path and clear skip/error classification.
- Live OpenAI proof remains opt-in and budget bounded.
- Large-screen UI proof is enough for this stage.

## Critical Path Risks
- EF audit store exists but production DI keeps using in-memory storage.
- A verification host beta accidentally becomes a generic execution runtime host.
- Selector gains fallback behavior or generic object payload dispatch.
- Manager diagnostics get exposed without authorization/audit/redaction gates.
- Live provider tests leak secrets or count skipped tests as live proof.
- Scheduler/workflow jobs invoke drivers directly instead of process-owned read-only verification boundaries.

## Validation Risks
- Prepared-stage and completed-stage validators must pass before execution or closure claims are trusted.
- Critical proof must include semantic positive and adversarial negative evidence, not only file existence or report prose.
- Live provider checks must remain opt-in, budget-bounded, and classified separately from deterministic tests.

## Reopen Triggers
- Any production code references `codex/bundles/<name>`.
- Core references drivers/modules/infrastructure/EF/UI/AgentFramework.
- Verification host starts mutating process state, transitions, finalizers, retries, claims, workspace, storage, Office/Graph, or CRM.
- Audit records are not persisted in production runtime mode.
- Live OpenAI proof is skipped but reported as live pass.
- UI/readback omits denial category, audit id/hash, no-mutation flags, or evidence reference counts.

