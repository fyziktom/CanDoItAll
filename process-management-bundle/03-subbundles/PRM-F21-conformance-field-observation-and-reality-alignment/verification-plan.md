# Verification plan — PRM-F21

## Expected verification outcomes

- Reviewers can record conformance observations against runs or process versions with structured deviation reasons.
- The system can cluster repeated unofficial loops, extra handoffs, and bypass patterns from journals for owner review.
- Observation notes support restricted visibility and privacy-safe governance handling; there is no unmanaged rumor registry.
- Process owners can convert deviation clusters into approved variants, fixes, or policy-breach investigations.
- Conformance reporting can show paper-versus-reality deltas by step, interface, owner, customer segment, or project.

## Automated tests

- Unit tests for new invariants and validation rules
- Integration tests for persistence and cross-module seams
- Component tests for editor or viewer surfaces where applicable
- Playwright coverage for the main happy path if new end-user flow is introduced

## Manual verification checklist

1. Record a conformance observation and verify restricted visibility works.
2. Replay a run with repeated unofficial loops and verify a deviation cluster is created.
3. Convert a deviation cluster into an improvement item or approved variant review.

## Regression concerns to watch

- Observation tooling becoming gossip or privacy risk
- Deviation clusters generated without evidence traceability