# Session handoff — SB02

## Repository state
- Branch: `maf-refactor`
- Starting SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Current SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Worktree: intentionally dirty with cumulative bundle preparation, SB00 characterization, SB01 implementation, and SB02 implementation/proof; no commit was requested.

## Completed
- Replaced the hard-coded provider catalog with required `IEnumerable<IAgentExecutionSourceAuthorityProvider>` injection.
- Moved project-structure, Projects, Processes, and live-Processes strategies and registrations to their publishing modules.
- Preserved duplicate-key rejection and fail-closed unknown-source behavior.
- Passed Release build, focused/neighboring tests, filtered Unit sweep, CodeAnalytics dependency proof, source guards, and architecture review.

## In progress
- None for SB02.

## Blockers/failing tests
- None owned by SB02.
- Eight intentionally red characterization tests remain locked to SB03 through SB07.

## Decisions
- Core owns the shared durable project-scope policy because it already owns the contextual durable access resolver; modules retain all source-specific parsing and fallback decisions.
- Providers are stateless singleton strategies registered with `TryAddEnumerable`.
- Separate Processes and live-Processes provider types make registrations distinguishable and idempotent.

## Changed files
- See `proof-manifest.json` and `proof/SB02/manifest.md`.

## Commands run
- See `proof/SB02/transcripts/`.

## Next exact action
- Run SB03 entry validation, then execute `SB03 — Effective tool policy context propagation` from its README and CODEX prompt.

## Risks not to forget
- Never allow a UI observation to become an authority grant.
- Reopen SB02 if a source-specific provider returns to Modules.AgentFramework or any project-reference cycle appears.
