# SB12 Proof Manifest

## Status

Completed.

## Goal

Refresh documentation and Codex skills so they describe the current process runtime, template pack, API skill, and MAF process automation boundary.

## Implementation Summary

- Added a source-backed operator troubleshooting map to `repo://src/CanDoItAll.Modules.Processes/README.md` covering artifact status projection, output grounding, manager resolution, run folder projection, and live-run profile policy.
- Expanded `repo://Templates/Processes/README.md` with a source-aligned authoring checklist for generic templates, final delivery grounding, live-run profiles, manager roles, and API skill parity.
- Added a current-run troubleshooting workflow to `repo://codex/skills/candoitall-api-processes/SKILL.md` and synced it to the active Codex skill root.
- Added MAF process automation notes to `repo://src/CanDoItAll.AgentFramework.Maf/README.md`.
- Updated the AgentFramework Core process capability matrix in `repo://src/CanDoItAll.AgentFramework.Core/README.md` with `processes_template_live_run_profiles_list`.
- Updated `repo://docs/api-control-plane.md` with live-run profile route and `freshRunPolicy` guidance.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/README.md | Documents current process troubleshooting and runtime readback boundaries. | bundle://proof/SB12/transcripts/changed-file-hashes.txt |
| repo://Templates/Processes/README.md | Documents source-aligned template authoring and live-run profile usage. | bundle://proof/SB12/transcripts/changed-file-hashes.txt |
| repo://codex/skills/candoitall-api-processes/SKILL.md | Documents current-run troubleshooting, fresh-run policy, and process tool parity. | bundle://proof/SB12/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Maf/README.md | Documents process automation notes and MAF 1.6 adopted/guarded surfaces. | bundle://proof/SB12/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.AgentFramework.Core/README.md | Keeps the process capability matrix aligned with the live-run profiles read tool. | bundle://proof/SB12/transcripts/changed-file-hashes.txt |
| repo://docs/api-control-plane.md | Updates process API usage notes for live-run profiles and `freshRunPolicy`. | bundle://proof/SB12/transcripts/changed-file-hashes.txt |

## Changed-file Hashes

- SHA-256 `6CFB7FA472AC90F8FFFC376D8A1CA2F8944DB0EC0F672B70A65310ACA50AADB4` repo://src/CanDoItAll.Modules.Processes/README.md
- SHA-256 `9AEEC15DF2D15DC94FE6A5A6B0874A76D51A4D7942694257B1149966E3B5042B` repo://Templates/Processes/README.md
- SHA-256 `9523684EECF476679E0E29025380F06CF9EA489DF72BF9DC38B9BA98087088CC` repo://codex/skills/candoitall-api-processes/SKILL.md
- SHA-256 `CC9135305AC60E749F35335F5F1876F82CFA5E542C0D1146D77DD9B307921DBE` repo://src/CanDoItAll.AgentFramework.Maf/README.md
- SHA-256 `769C7E7DD6E7C24113726B91BE55244F25917C904F03D7C6F089683C0404DAB1` repo://src/CanDoItAll.AgentFramework.Core/README.md
- SHA-256 `6429CE909B0731B2EF0504D9ED56463D638525C6C12DFBF45DC8479FA04317CE` repo://docs/api-control-plane.md
- SHA-256 `9523684EECF476679E0E29025380F06CF9EA489DF72BF9DC38B9BA98087088CC` active Codex `candoitall-api-processes` skill copy

## Failing-first or adversarial proof

`bundle://proof/SB12/transcripts/failing-first.txt`

- Rejects stale docs that refer to MAF 1.0, active Processes MCP control, or seeded baseline artifacts/transitions as live delivery evidence.

## Passing proof

`bundle://proof/SB12/transcripts/passing.txt`

- `git diff --check` passed for SB12 documentation files.
- Active `candoitall-api-processes` skill hash matches the repository skill hash.

## Source assertions

`bundle://proof/SB12/transcripts/source-assertions.txt`

## Anti-stub audit

`bundle://proof/SB12/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`bundle://proof/SB12/transcripts/changed-file-hashes.txt`

## Closure Validator

`bundle://proof/SB12/transcripts/closure-validator.txt` records no SB12 validator findings.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Processes operator troubleshooting map | Processes module README. | Process maintainers, operators, SB13 observability work, and SB18 red-team. | Updated after SB03-SB11 runtime/API proof so docs cite current services and readbacks. | Stale-control and seeded-evidence rejection in `bundle://proof/SB12/transcripts/failing-first.txt`. |
| Template authoring checklist | Process template README. | Template authors and SB14/SB17 template parity work. | Documents source-aligned generic templates, final delivery grounding, live-run profiles, and manager roles. | Anti-stub and source assertions in `bundle://proof/SB12/transcripts/anti-stub-audit.txt` and `bundle://proof/SB12/transcripts/source-assertions.txt`. |
| Active process API skill | Repo skill synced to active Codex skill root. | Human and agent users of `candoitall-api-processes`. | Current-run troubleshooting and live-run profile guidance copied to active skill root with matching hash. | Skill sync proof in `bundle://proof/SB12/transcripts/skill-sync.txt`. |
| MAF process automation notes | MAF README and Core capability matrix. | AgentFramework maintainers and process automation agents. | Documents process tool read/mutation boundaries, MAF 1.6 proof slices, guarded surfaces, and live-run profiles read tool. | Source assertions and stale MAF 1.0 rejection in `bundle://proof/SB12/transcripts/source-assertions.txt` and `bundle://proof/SB12/transcripts/failing-first.txt`. |
