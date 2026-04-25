# Structured Input

## Objectives

- Prove what currently works in the mock-agent path.
- Test enough of the process service and process runtime path to expose the first blockers.
- Map why the current system cannot yet finish the calculator multi-role process end to end.
- Prepare an implementation-ready repair bundle named `process-run-with-agents-fix`.

## Hard Constraints

- Do not connect to real LLM agents for this repair path.
- Keep mock-agent execution behind `AgentFramework:ProcessMockAgents:Enabled`.
- Preserve strict process governance; do not add silent fallback behavior that hides errors.
- Keep changes small and aligned with existing C#/.NET architecture.
- Treat this bundle as planning only. Production code fixes belong to execution subbundles.

## Assumptions

- The target production-like process template is closest to `software-delivery`, but it currently has no QA repair branch.
- The AI governance template `ai-assisted-change-delivery` has AI-specific roles and branch outcomes, but it also does not model a developer repair loop back to QA.
- A deterministic calculator repair process may need a dedicated test fixture/template before it is safe to adapt the generic templates.

## Open Questions For Implementation

- Should the deterministic calculator process be a test-only builder, a template-pack process, or both?
- Should mock role aliases be built into `ProcessMockAgentCatalog`, or should template role keys be adjusted for the mock process only?
- Should eager automation dispatch after `StartRunAsync` become awaitable/test-controllable, or should tests disable the eager kickoff and drive the durable outbox explicitly?
