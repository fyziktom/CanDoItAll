# SB15 Proof Manifest

## Status

Completed.

## Goal

Refactor the proof and test harness to avoid broad timeouts.

## Shipped behavior

- `bundle://scripts/validation-commands.md` is now the SB15 proof-harness catalog instead of a timeout-prone undifferentiated validation note.
- The catalog splits final proof into named suites: unit policy, integration runtime/artifacts, integration template governance, integration process API/MAF, component process UI, opt-in live PostgreSQL, opt-in browser, and static audits.
- The default commands isolate output paths under `repo://artifacts/codex-sb15-unit`, `repo://artifacts/codex-sb15-integration`, and `repo://artifacts/codex-sb15-components`, require `-p:CopyRepositoryTemplatesToOutput=false` for template-loading integration slices, and use `--no-build` after the first suite build.
- Live/PostgreSQL and browser validation are explicitly opt-in and must write proof into the owning subbundle folder instead of being mixed into smoke validation.
- The quarantine policy disallows `Category=Quarantined`, `Category=LongRunning`, and `Category=LiveProcess` tests as default release-closure proof unless an owning transcript records environment, reason, and owner.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://codex/bundles/processes-post-live-run-hardening-docs-v1/scripts/validation-commands.md | Adds named timeout-risk classified validation suites, isolated output paths, live/browser separation, static audits, and quarantine policy. | bundle://proof/SB15/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj | Suite target for policy/runtime guardrail proof. | bundle://proof/SB15/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | Suite target for runtime, template, API, MAF, and live PostgreSQL proof. | bundle://proof/SB15/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj | Suite target for split process component proof. | bundle://proof/SB15/transcripts/changed-file-hashes.txt |

## SHA-256 proof snapshot

```text
011BD21B518EB5D559FF4D3A7C10D9CAA88DA1A10857D3D80F0E444688985744  codex/bundles/processes-post-live-run-hardening-docs-v1/scripts/validation-commands.md
77E5D6C863D86F3D01433378541EFB1BCFF8A792ECC18C4B1F4F60F879B885EC  tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
ECC931592EAB8735528F1C025198673B2EE1E4D7A7744421DC238AEFF6970238  tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
DB4AA08F703C3698E8389D6520F4E9D07E2ADC696E586CB9AC1C27D07C88FE4B  tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj
```

## Failing-first or adversarial proof

`proof/SB15/transcripts/failing-first.txt`

## Passing proof

`proof/SB15/transcripts/passing.txt`

## Source assertions

`proof/SB15/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB15/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB15/transcripts/changed-file-hashes.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB15 proof-harness catalog | `bundle://scripts/validation-commands.md`. | SB18 final red-team closure, subbundle validators, and future Codex operators running process hardening proof. | Maintained with the bundle and invoked when final validation must be split into stable suites. | Pre-change source assertion exits 1 in `bundle://proof/SB15/transcripts/failing-first.txt`; source assertions prove named suites and opt-in boundaries. |
| Isolated suite output paths | SB15 catalog commands. | `dotnet build` and `dotnet test` proof runs. | Prevents locked live web output and stale build artifacts from invalidating validation, then supports `--no-build` test proof. | Passing transcripts prove unit, integration, and component slices run against `repo://artifacts/codex-sb15-unit`, `repo://artifacts/codex-sb15-integration`, and `repo://artifacts/codex-sb15-components`; broad component command timeout is recorded as the rejected path. |
| Timeout-risk and opt-in classification | SB15 suite table and quarantine policy. | SB18 release-readiness gate. | Keeps live/PostgreSQL, browser, long-running, and quarantined suites out of default closure unless explicitly owned by proof. | Adversarial broad component timeout exits 124, while the split component slice passes in `bundle://proof/SB15/transcripts/passing.txt`. |
