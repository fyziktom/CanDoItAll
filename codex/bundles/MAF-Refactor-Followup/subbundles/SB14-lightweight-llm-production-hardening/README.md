# SB14: Production-harden the lightweight LLM port and move neutral workflow invocation outward

## Metadata

- Phase: F — Lightweight inference foundation
- Depends on: `SB13`
- Checkpoint: No
- Optional: No
- Baseline: `fyziktom/CanDoItAll` `maf-refactor` @ `9e47a332fa9d329422ff616a0e0b6a97a22933c9`
- Primary executor: Claude Code / Claude Fable 5
- Reasoning profile: deepest available (`xHigh` intent)
- Compatible executor: Codex or another high-capability coding model using the included prompt
- Review findings addressed: `FR-011`

## Goal

Make ILlmInvocationPort safe for workflows today and a stable foundation for ordinary LLM chat later, without agent/session/tool construction.

## Required context

1. Read `../../00-READ-ME-FIRST.md`, `../../01-REVIEW-VERDICT.md`, `../../02-FINDINGS-REGISTER.md`, and `../../03-EXECUTION-ORDER.md`.
2. Read the architecture and plan documents relevant to this subbundle.
3. Read `../../sharedinfo/required-skills.md` and use installed skills.
4. Confirm every dependency is explicitly unlocked.
5. Confirm current branch/HEAD and refresh the evidence map if it differs from the baseline.
6. Create `proof/proof-manifest.json` and `proof/SESSION-HANDOFF.md` before risky edits.

## Detailed tasks

1. Make request collections and attachment bytes immutable/defensively copied and enforce attachment count, per-item size, aggregate size, content type, and message length limits.
2. Clarify model selection: either allow empty model for provider default or require explicit model consistently; remove dead fallback behavior.
3. Validate ordered messages and define system-message placement policy without silently changing semantic order. Require at least one user input for ordinary invocation unless a named use case permits otherwise.
4. Add operation/correlation ID, absolute deadline or bounded timeout, and cancellation semantics.
5. Introduce typed sanitized LlmInvocationException/failure categories while retaining protected inner diagnostics. Raw provider exception messages must not cross public/workflow boundaries.
6. Add one bounded retry for a fully empty, non-actionable stateless response. Never retry after any tool/hosted action because this port has no tools by contract.
7. Map cached/reasoning/total usage consistently across OpenAI, Azure, Ollama, and future providers.
8. Move MafWorkflowLlmComponentInvoker to a neutral workflow runtime/provider project and move ILlmInvocationPort registration into Llm.ProviderRuntime/hosting. The MAF workflow backend may depend on the neutral invoker contract, not own it.
9. Add provider-driver and workflow integration parity tests, including empty response, malformed JSON, timeout, cancellation, and sanitized failures.

## C# architecture impact

This is architecture-sensitive work. Map current owner, target owner, contracts, implementations, composition root, dependency direction, test seam, and old behavior to delete before editing. New interfaces without production consumption do not count as completion.

## Testability contract

- immutability/limits
- provider default model
- empty retry once
- no retry after content
- failure sanitization
- workflow no-agent guard

Every behavior change requires at least one negative test. Extracted behavior must be directly testable without constructing the original broad runtime/workspace graph unless the test is explicitly an integration smoke.

## Constraints

- Keep source-code comments and identifiers in English.
- Do not add partial-class architecture, nested architecture owners, broad Helpers/Managers, or a Common dumping ground.
- Do not let UI observation, route, prompt text, payload JSON, or current navigation grant authority.
- Do not recapture current UI context or authority during approval continuation.
- Do not duplicate provider, tool, process, or persistence side effects for comparison.
- Do not restore product/process semantics or product module references to MAF.
- Do not make lightweight LLM calls use the full agent runtime.
- Do not add new accepted test failures or exclusions.
- Do not commit, push, or open a PR unless explicitly requested.

## High-risk points

- Do not introduce provider SDK types into Llm.Abstractions.
- Do not use the full agent runtime as fallback.
- Do not double-count usage across retry attempts.

## Required architecture proof

- before/after responsibility and dependency map
- CodeAnalytics snapshot/dependency/cycle evidence when available
- changed files/projects
- failing-first and negative tests
- focused build/test transcript
- source assertions proving the old production path no longer grants/owns the responsibility
- rollback and retained compatibility readers/selectors
- completed proof manifest and durable session handoff

## Acceptance criteria

- The goal is satisfied in the production call chain, not only in new contracts/tests.
- Authority, scope, state, approval, and process invariants remain fail-closed.
- No new reverse references, hidden service location, partial-class growth, or duplicate side-effecting paths exist.
- Focused tests, architecture guards, and all checkpoint-required validation pass.

## Stop and repair conditions

Mark this subbundle Blocked if the design requires UI-derived authority, current-context recapture, mixed workspace bundles, silent state replay, process logic in MAF, a project-reference cycle, or a test exclusion. Continue other safe in-scope work and record exact evidence; do not weaken the invariant.

## Downstream unlock

SB15 optional and SB16 required.

## Required closure output

- Status: Completed | Blocked | Completed with bounded non-blocking follow-up
- Start/result commit and changed files/projects
- Implemented boundary changes
- Build/test/guard commands and results
- Failing-first and negative proof
- Architecture/dependency/source-of-truth proof
- Bugs found and owner-boundary fixes
- Compatibility/rollback state
- Downstream unlock decision
- Path to updated `proof/SESSION-HANDOFF.md`
