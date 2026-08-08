# Session handoff — SB08

## Repository state

- Branch: `maf-refactor`
- Starting SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Current SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Worktree: dirty with cumulative SB00-SB08 implementation and proof changes; no commit was requested or created.

## Completed

- Removed ordinary-conversation activation from the AgentFramework production module.
- Preserved the abstractions, implementation, project/solution entries, opt-in extension, and isolated composition test.
- Strengthened the production guard to scan C#, Razor, and CSHTML sources for registration, injection, or direct store consumption.
- Kept all agent and workflow paths unchanged during SB08.
- Verified the future activation requirements for profile identity, profile generation, switch fencing, product ownership, retention, and integration testing.
- Passed 27 focused tests, five neighboring module-composition tests, and a Release solution build with zero warnings and errors.

## In progress

- None for SB08.

## Blockers/failing tests

- None. The deliberate failing-first non-activation characterization failed before the production edit and passes afterward.

## Decisions

- The foundation remains opt-in and dormant; no fallback profile resolver was added.
- The existing module project reference remains because the bundle explicitly preserves project and solution references.
- Future activation is a separate product feature and must fence current profile identity and generation on every storage-authorized operation.

## Changed files

- See `proof-manifest.json` and `../../proof/SB08/manifest.md`.

## Commands run

- See `../../proof/SB08/transcripts`.

## Next exact action

- Enter SB09 and run the complete regression, architecture, bundle, and merge-readiness gates.

## Risks not to forget

- Do not reintroduce production registration until a product-owned API/UI and profile-switch fencing exist.
- Keep the positive isolated composition test; dormant does not mean dead or untested.
