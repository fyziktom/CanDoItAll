# CanDoItAll MAF refactor post-implementation review and follow-up bundle

## Purpose

This bundle is a **corrective follow-up** for `fyziktom/CanDoItAll` branch `maf-refactor` at commit `9e47a332fa9d329422ff616a0e0b6a97a22933c9`. It reviews the implementation produced from the original MAF/context refactor bundle and defines the remaining work required before the branch is considered safe to merge.

The implementation is a substantial improvement. It successfully introduces narrow runtime ports, removes direct MAF references to product modules, moves process artifact recovery into Processes, creates a direct lightweight LLM port, and adds immutable floating-turn context concepts. The branch is **not yet merge-ready**, because several new contracts are not wired as the authoritative production path and a few scope/state/lifetime defects remain.

## Independent review status

- Repository: `fyziktom/CanDoItAll`
- Reviewed branch: `maf-refactor`
- Reviewed HEAD: `9e47a332fa9d329422ff616a0e0b6a97a22933c9`
- Compared development/merge base: `26da0c55861e5d4e6ca325e561f3f4612aa93266`
- Branch relation at preparation time: ahead by 2, behind by 0
- GitHub checks/workflow runs visible for reviewed HEAD: none
- Review method: GitHub source/diff inspection plus review of branch-produced proof artifacts
- Local build/test reproduction in the preparation environment: unavailable because the repository was not locally mounted and outbound clone access was unavailable

Therefore `SB00` must rerun every build/test/CodeAnalytics proof against the exact current HEAD before any fix is accepted.

## Merge recommendation

**Blocked pending corrective work.** The branch should not be merged while FR-001 through FR-007 remain unresolved. FR-008 through FR-014 are required release hardening, except `SB15`, which is explicitly optional and may be deferred.

## How to execute

1. Read `01-REVIEW-VERDICT.md`, `02-FINDINGS-REGISTER.md`, and `03-EXECUTION-ORDER.md`.
2. Execute one subbundle at a time. Never combine checkpoints with implementation work.
3. Use Claude Code with Fable 5 and deepest available reasoning as the primary executor; the prompts are model-neutral enough for Codex or another strong coding agent.
4. Use installed SharedInfo C# architecture skills and CodeAnalytics MCP before broad manual editing.
5. Keep all source-code comments in English.
6. Do not commit/push unless the operator explicitly requests it.
7. Persist every session into `proof/SESSION-HANDOFF.md`; do not depend on chat memory.

## Non-negotiable invariants

- UI observation never grants authority.
- One admitted authority snapshot controls scope, capability composition, and invocation policy.
- Approval continuation uses the original turn context and authority, never current UI state.
- One run owns one complete workspace identity and one disposable service bundle.
- MAF maps SDK behavior; it does not own process semantics or product authorization.
- Runtime state restores only through a named compatibility/migration decision.
- Lightweight LLM calls never construct agents, sessions, tools, memory, or workspace authority.
- No dual side-effecting shadow execution.
