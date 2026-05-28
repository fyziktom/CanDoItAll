# SB17 Proof Manifest

## Status

Completed.

## Goal

Docs and template parity checkpoint.

## Shipped behavior

- `Templates/Processes/README.md` now lists the source-aligned enum values that template authors must preserve for `ProcessStepOperation`, `ProcessStepTargetScope`, `ProcessStepBlockCause`, `ProcessStepRecoveryOption`, and `ProcessArtifactExpectationSatisfactionStatus`.
- `codex/skills/candoitall-api-processes/SKILL.md` now gives API operators the exact source enum names for operation target scopes, recovery options, and artifact expectation satisfaction statuses instead of relying only on prose labels.
- The active Codex skill copy at `C:\Users\lucys\.codex\skills\candoitall-api-processes\SKILL.md` was synchronized to the repo skill copy and hash-verified.
- Template governance tests still pass after the docs/skill parity update.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://Templates/Processes/README.md | Adds source-aligned enum parity guidance for template authors. | bundle://proof/SB17/transcripts/changed-file-hashes.txt |
| repo://codex/skills/candoitall-api-processes/SKILL.md | Adds source-aligned API skill guidance for operation target scopes, recovery options, and artifact statuses. | bundle://proof/SB17/transcripts/changed-file-hashes.txt |
| C:\Users\lucys\.codex\skills\candoitall-api-processes\SKILL.md | Active skill root copy synchronized from repo skill. | bundle://proof/SB17/transcripts/changed-file-hashes.txt |

## SHA-256 proof snapshot

```text
41B0B533A5697986D71C68504545C417FEB7F40C911A4072CEA5C81612BF5893  Templates/Processes/README.md
F72CE56AC7274F1109B2E96A5D1FE9F44CDE047097181DC68BD1BA2F0E5C4842  codex/skills/candoitall-api-processes/SKILL.md
F72CE56AC7274F1109B2E96A5D1FE9F44CDE047097181DC68BD1BA2F0E5C4842  C:/Users/lucys/.codex/skills/candoitall-api-processes/SKILL.md
1DE16A743D7884C5076FB986FCD045E3EA4199B3188A9A32AEB975AD8D8CEE0E  src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs
3A21EC7A087AB90EC5880820521C1328EFD1D9BDBEC586E67289D262E8116428  src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
```

## Failing-first or adversarial proof

`proof/SB17/transcripts/failing-first.txt`

## Passing proof

`proof/SB17/transcripts/passing.txt`

## Source assertions

`proof/SB17/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB17/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB17/transcripts/changed-file-hashes.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Source-aligned template authoring enum guidance | `repo://Templates/Processes/README.md` from `ProcessDefinitionEnums` and `ProcessRuntimeViewModels`. | Template authors, governance tests, SB18 release readiness. | Updated when operation, target scope, block cause, recovery option, or artifact satisfaction enums change. | Pre-change exact enum markers are absent in `bundle://proof/SB17/transcripts/failing-first.txt`; parity script passes in `bundle://proof/SB17/transcripts/passing.txt`. |
| Source-aligned process API skill guidance | `repo://codex/skills/candoitall-api-processes/SKILL.md` and active skill copy. | Codex/API operators managing processes through HTTP routes. | Synced into active skill root whenever repo skill changes. | Hash parity in `bundle://proof/SB17/transcripts/passing.txt` proves active skill sync. |
| Template governance runtime proof | `ProcessTemplateGovernanceTests`. | Template pack maintainers and SB18 final closure. | Runs from an isolated SB17 integration output path with repository templates loaded from source. | Passing 10/10 governance tests in `bundle://proof/SB17/transcripts/passing.txt`. |
