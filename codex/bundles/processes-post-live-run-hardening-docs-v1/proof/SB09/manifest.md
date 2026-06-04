# SB09 Proof Manifest

## Status

Completed.

## Goal

Update template pack and live-run profiles after real-run learning with typed fresh-run governance that prevents seeded transitions or seeded artifacts from being treated as live delivery evidence.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://Templates/Processes/README.md | Documents live-run profile governance, current-run evidence checks, and the actual governance-test validation command. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |
| repo://Templates/Processes/manifest.json | Bumps the template pack version for live-run governance and keeps the live-run profile catalog wired. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |
| repo://Templates/Processes/seed-catalog/live-run-profiles.json | Adds explicit `FreshRunPolicy` to the Blazor WASM PWA live-run profile. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |
| repo://Templates/Processes/seed-catalog/baseline-scenarios.json | Aligns the Blazor implementation baseline contract exercise with the template's `LaunchRuntime` operation and removes a misleading pending marker. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs | Adds the typed `ProcessTemplateLiveRunFreshRunPolicy` model. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs | Strengthens live-run profile governance assertions for fresh-run policy and current-run evidence checks. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |

## Proof-bearing Source Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs | Confirms live-run profiles are loaded from the manifest path into the pack model. | bundle://proof/SB09/transcripts/changed-file-hashes.txt |

## Failing-first Or Adversarial Proof

- bundle://proof/SB09/transcripts/failing-first.txt records non-zero searches proving live-run profiles do not carry `Transitions` or `Artifacts` seed collections and that the README no longer points to a missing `validate_process_template_pack.py` script. It also records the targeted fresh-run profile governance test.

## Passing Proof

- bundle://proof/SB09/transcripts/passing.txt records 10 passing `ProcessTemplateGovernanceTests`, covering live-run profiles, baseline scenarios, typed contracts, artifact mapping, and vocabulary support.

## Source Assertions

- bundle://proof/SB09/transcripts/source-assertions.txt records the typed fresh-run policy model, JSON seed policy, manifest version, README governance text, strengthened tests, baseline `LaunchRuntime` exercise, and loader live-run profile path.

## Anti-stub Audit

- bundle://proof/SB09/transcripts/anti-stub-audit.txt records no TODO, pending, stub, or `NotImplementedException` markers in the SB09 changed template, model, docs, and test files.

## Changed-file Hashes

- SHA-256 `B35D22E6AA0BBEA0202AA7DBA4950EAB8076486C3D1F1C30F0EBF0226D930818` repo://Templates/Processes/README.md
- SHA-256 `FB4DFA08C6DC1E6C10A6449D5EC82D678C8B5BA1192EAFD529ED463F16E3B8F9` repo://Templates/Processes/manifest.json
- SHA-256 `685DBFBC3707886BCBD36C3EB3255889C95F5EB42A42DF102E071D42E3F62239` repo://Templates/Processes/seed-catalog/live-run-profiles.json
- SHA-256 `021D61DCDB4066F3CF93AB73C1A6A663B1F7E14A5E3CFDAA224404463C5D3F2D` repo://Templates/Processes/seed-catalog/baseline-scenarios.json
- SHA-256 `827D726B8D2581BA265CB51D2EED9C1D4623B315852B52D5C64A815F3CBF6624` repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs
- SHA-256 `73E802834B4A8620671CD9C5E8A8DA0718017FD5DCE89C850B9D356C57E3BE29` repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs
- SHA-256 `BE973E2E453765DE1EA33B242F591EAD4BA857EDC2B5146FF8F2B7A0BE145D96` repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs
- bundle://proof/SB09/transcripts/changed-file-hashes.txt records the command transcript for these hashes.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Typed live-run fresh-run policy | repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs `ProcessTemplateLiveRunFreshRunPolicy`; source proof bundle://proof/SB09/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs and template/API consumers of `ProcessTemplatePack.LiveRunProfiles` | Loads explicit fresh-run requirements, seeded transition/artifact rejection, pre-dispatch checks, evidence checks, and writeback guidance with the live-run profile | bundle://proof/SB09/transcripts/failing-first.txt proves live-run profiles do not define seed `Transitions` or `Artifacts` collections |
| Blazor WASM PWA live-run profile governance | repo://Templates/Processes/seed-catalog/live-run-profiles.json and repo://Templates/Processes/README.md | Fresh UI-driven process launch guidance and downstream SB14/SB17 template/docs work | Requires concrete run-request topic, current-run evidence checks, no seeded state, and project-structure writeback from current-run managed output | bundle://proof/SB09/transcripts/passing.txt proves profile policy, demo-topic rejection, and absence of `Transitions`/`Artifacts` model properties |
| Template baseline contract alignment | repo://Templates/Processes/seed-catalog/baseline-scenarios.json | `ProcessTemplateGovernanceTests` and future template proof harnesses | Aligns baseline contract exercises with declared step operations, including `LaunchRuntime` for the implementation step | bundle://proof/SB09/transcripts/passing.txt proves all 10 governance tests pass after the correction |

## Browser Validation

N/A. SB09 changed template JSON, typed template models, tests, and README documentation. It did not change UI markup, CSS, routes, layout, or visible rendering components.

## Closure

- SB09-INV-001 is satisfied by repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs, repo://Templates/Processes/seed-catalog/live-run-profiles.json, and bundle://proof/SB09/transcripts/passing.txt.
- Seeded transition/artifact rejection is satisfied by bundle://proof/SB09/transcripts/failing-first.txt.
- SB14 and SB17 may rely on current live-run profile governance after this gate.
