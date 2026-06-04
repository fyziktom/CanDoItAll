# SB15 Semantic Invariants

## Invariants

- Invariant ID: `SB15-INV-001`
- Source raw note: RN15 - refactor proof/test harness to avoid broad timeout-prone validation and preserve durable closure proof.
- Expected behavior: Final proof is split into named, timeout-risk classified suites with isolated outputs, no-build reruns, template-copy isolation, opt-in live/browser validation, static audits, quarantine policy, and transcript destinations.
- Disallowed shallow implementation: leaving a single mega command as release proof, mixing live/browser tests into default smoke validation, relying on locked web output directories, omitting transcript paths, or documenting tests without executing the owned slices.
- Failing-first test: bundle://proof/SB15/transcripts/failing-first.txt records pre-change absence of the SB15 suite catalog and records the broad `ProcessWorkspaceTests` component command timing out with exit 124.
- Passing test: bundle://proof/SB15/transcripts/passing.txt records isolated build/test proof for unit policy, runtime/artifact integration, template governance, process API/MAF integration, and split process component suites.
- Changed source files: repo://codex/bundles/processes-post-live-run-hardening-docs-v1/scripts/validation-commands.md.
- Production assertions: suite commands use `repo://artifacts/codex-sb15-*` output paths, pass `-p:CopyRepositoryTemplatesToOutput=false` for integration template loading, and keep `SB15-LIVE-POSTGRES` and `SB15-BROWSER` as opt-in proof.
- Red-team negative case: A future SB18 closure cannot claim release readiness from a broad command that times out, from a live/browser run without owned environment proof, or from quarantined/long-running categories as default proof.
- Downstream dependency check: SB18 can now cite named suite transcripts instead of reintroducing timeout-prone broad commands.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Named validation suite catalog | `bundle://scripts/validation-commands.md`. | SB18 final red-team closure and future process hardening validators. | Updated as proof coverage changes, with explicit suite ID, project, timeout-risk class, closure default, transcript path, and purpose. | Source absence and broad timeout are recorded in `bundle://proof/SB15/transcripts/failing-first.txt`. |
| Isolated output path convention | SB15 catalog build/test commands. | Local proof runners and CI-like closure scripts. | Builds each project into `repo://artifacts/codex-sb15-*`, then runs tests with `--no-build` against that path. | Passing proof records successful isolated unit, integration, and component runs. |
| Live/browser/quarantine boundary | SB15 opt-in and quarantine sections. | Operators deciding whether a proof transcript is release-closure evidence. | Live/PostgreSQL and browser validation must be owned by a subbundle proof folder; quarantined, long-running, and live-process tests are excluded from default closure. | Static source assertions prove the policy text and the broad component timeout proves why high-risk suites must be split. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB15/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB15/transcripts/passing.txt.
- Source assertions: bundle://proof/SB15/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB15/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB15/transcripts/changed-file-hashes.txt.
