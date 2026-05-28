# SB14 Proof Manifest

## Status

Completed.

## Goal

Protect generic Processes behavior.

## Shipped behavior

- `Templates/Processes/seed-catalog/baseline-scenarios.json` now includes `baseline-agent-training-and-improvement`, a nonsoftware agent-improvement scenario over the existing `ai-assisted-change-delivery` process skeleton.
- The new scenario exercises bounded delegation, trace capture, evaluation, safety review, rework routing, typed operation contracts, branch selection, and recovery metadata without adding Blazor/Tetris/project-specific assumptions.
- `ProcessTemplateGovernanceTests` now requires the agent-improvement baseline in the typed baseline scenario matrix, so future template-pack changes must preserve its contracts, branch metadata, and recovery exercises.
- `Templates/Processes/manifest.json` already contains the `ai-assisted-change-delivery` process skeleton; SB14 proves it is now reachable from a baseline scenario instead of being only a standalone template.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://Templates/Processes/seed-catalog/baseline-scenarios.json | Adds the agent training/improvement baseline scenario. | bundle://proof/SB14/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs | Extends the SB14 typed baseline matrix to cover the agent-improvement scenario. | bundle://proof/SB14/transcripts/changed-file-hashes.txt |
| repo://Templates/Processes/manifest.json | Source assertion that the `ai-assisted-change-delivery` process skeleton exists. | bundle://proof/SB14/transcripts/changed-file-hashes.txt |

## Failing-first or adversarial proof

`proof/SB14/transcripts/failing-first.txt`

## Passing proof

`proof/SB14/transcripts/passing.txt`

## Source assertions

`proof/SB14/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB14/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB14/transcripts/changed-file-hashes.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Agent training/improvement baseline scenario | Template seed catalog. | Process development seeding, MAF/API baseline scenario readers, governance tests, and SB18 red-team. | Loaded with baseline scenarios and seeded as runtime state when development seeds are installed. | Pre-change source absence in `bundle://proof/SB14/transcripts/failing-first.txt`; contract/recovery proof in `bundle://proof/SB14/transcripts/passing.txt`. |
| Typed baseline governance matrix | `ProcessTemplateGovernanceTests`. | Template maintainers and CI. | Fails when required baseline scenarios disappear, branch selections drift from template branches, or contract/recovery metadata no longer matches process definitions. | Focused source absence and passing governance test in `bundle://proof/SB14/transcripts/failing-first.txt` and `bundle://proof/SB14/transcripts/passing.txt`. |
| Existing AI-assisted process skeleton | Template manifest and `processes/ai-assisted-change-delivery`. | Agent-improvement baseline scenario and process template projection. | Reused as the skeleton for SB14 instead of creating a duplicate process family. | Source assertion proves the manifest entry; anti-stub audit rejects placeholder implementation markers. |
