# Acceptance evidence — SB03

- [x] Every public LLM Chat application operation captures profile identity before its first read.
- [x] All repositories, provider resolution, transcript commands, and audit writes use the captured operation scope.
- [x] A profile switch prevents every subsequent old-generation durable commit.
- [x] A switch during provider execution yields deterministic non-success or RecoveryRequired with retained usage evidence.
- [x] No current-profile DbContext or provider lease is cached across operations.

## Required semantic proof

- Intended case: every `ILlmChat*ApplicationService` call acquires one immutable host-root profile ID,
  fingerprint, and generation before entering its application service, then carries that identity through
  repository, transcript, provider, audit, commit, and authoritative return.
- Negative/race/crash/failure case: a deterministic switch after provider dispatch but before finalization
  returns typed `RuntimeProfileChanged`; the committed successful invocation audit retains 10 input,
  4 output, and 1 cached-input token while the operation remains Running with its active turn and no
  assistant message. Every later write through the stale host is rejected.
- Why the old implementation would fail this proof: at pre-SB03 commit `61abf5bc3`, the exact public
  conversation query never acquires a runtime lease and returns success after its first read switches the
  profile. The regression fails 0/1 at the lease-acquired assertion.
- Exact source owner: `LlmChatProfileScopeRunner`, internal profile-scoped application decorators,
  `DatabaseProfileLlmChatCommitFence`, and `DatabaseRuntimeState`.
- Exact command(s): focused historical unit regression, focused 12-case unit fence/composition slice,
  and focused real-host PostgreSQL switch-before-finalization API test.
- Actual result: expected historical red 0/1; current unit 12/12 and PostgreSQL/API 1/1.
- Evidence artifact: `proof/SB03/manifest.md` and its transcript inventory.
- Commit SHA: `96f054905eecd33e04228e7837ae7850e3eeeeb4`.
