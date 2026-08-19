# SB09 governed proof manifest

- Final aggregate diff: `proof/SB09/final-changed-files-and-ranges.json` (113 files; actual one-based ranges).
- Impact analysis: `proof/SB09/final-impacted-tests-request.json` and `proof/SB09/final-impacted-tests-response.json`; Components workspace healthy, 113 projects, 922 source tests, AllSuppliedSuites.
- Test execution: `proof/SB09/final-test-execution.json`; affected Components 990/990, Stable 8,284 passed with three classified unrelated LlmChats failures and two expected skips.
- Builds: `proof/SB09/final-build-execution.json`; all affected projects pass.
- Guards: `proof/SB09/final-source-and-dependency-guards.txt`; repository, phase, neutral dependency, partial, service-location, and direction checks pass.
- Architecture: CP4 snapshot `snap-20260816142006-84a4f698` remains current because SB09 made no production edit; no project cycle or reverse dependency.
- Browser: `proof/SB09/final-ui-parity.md` and `proof/SB09/browser/`; real main and floating sends, floating lifecycle, settings save, and Process consumer pass with zero console warnings/errors.
- Broad gate: `proof/SB09/broad-gate-decision.md`.
- User handoff: `proof/SB09/user-regression-handoff.md` and `reviews/user-regression-handoff.md`.
- Semantic invariants: `proof/SB09/semantic-invariants.md`.
- Decision: CP5 passes with unrelated Stable findings; terminal state is `awaiting-user-agent-chat-regression` and Simple Chat UI remains inactive.
