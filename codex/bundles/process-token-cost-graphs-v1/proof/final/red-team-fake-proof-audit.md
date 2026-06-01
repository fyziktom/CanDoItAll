# Final Red-Team Fake-Proof Audit

Run label: 2026-06-01 final bundle proof audit

## Audit Scope

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- `bundle://proof/SB02/semantic-invariants.md`
- `bundle://proof/SB03/semantic-invariants.md`

## Fake-Proof Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Template-only proof rejected | Pass | Each manifest cites source assertions and test transcripts, not only status prose. |
| Fixture-only accounting rejected | Pass | SB01 integration test exercises execution-run persistence with fake runtime provider usage and asserts persisted metric values. |
| Cached input counted but not priced rejected | Pass | SB01 unit proof covers resolved cost with cached input tokens. |
| Completed-run graph disappearing rejected | Pass | SB02 component proof creates a completed priced run and asserts one-day history includes it after refresh. |
| Eager all-runs graph load rejected | Pass | SB03 component proof asserts no process graph snapshot exists until explicit button click. |
| Run graph using all-runs data rejected | Pass | SB03 component proof asserts selected-run graph snapshot loads after run graph tab activation for the selected run. |
| Browser proof faked | Pass with blocker | Browser artifacts are explicitly marked as blocked for updated UI because the isolated web host fails local PostgreSQL baseline validation. No screenshot is claimed as updated UI proof. |

## Residual Finding

The implementation proof is behavior-level for backend, observation, and Blazor component flows. Real browser screenshot proof remains blocked by the local database profile baseline mismatch and must be rerun after the local PostgreSQL profile is migrated or a disposable profile is available.
