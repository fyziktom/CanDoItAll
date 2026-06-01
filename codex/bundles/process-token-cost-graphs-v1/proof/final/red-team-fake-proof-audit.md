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
| Browser proof faked | Pass | Browser screenshots were captured from the updated app hosted on `http://localhost:5034` with disposable PostgreSQL database `candoitall_codex_graphs_20260601`. Historical blocker notes remain retained but are no longer claimed as final proof. |

## Residual Finding

The implementation proof now includes backend, observation, Blazor component, and browser screenshot evidence. The default local PostgreSQL profile still has a baseline mismatch, so the browser proof intentionally used a disposable PostgreSQL profile.
