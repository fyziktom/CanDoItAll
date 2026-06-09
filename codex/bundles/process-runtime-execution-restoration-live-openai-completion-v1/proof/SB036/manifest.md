# SB036 Proof Manifest

## Status
Completed.

## Objective
Gate L: prove manager diagnostics without mutation.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 manager diagnostics subset.
- Critical invariant contract: `bundle://proof/SB036/semantic-invariants.md`
- Downstream dependency: SB037-SB039 launch API compatibility may start after manager diagnostics are read-only, source-backed, and guarded against runtime driver host drift.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs` | `000b198b58e446d54ed0d7b9a3e24d511e1fb0aebf50ae8d246455a5fef388fe` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `25568fce81edb5cd150e5aaa8cfe2f1078e5afe1b5c36495db5e587cf35f81c5` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB034/README.md` | `e1e5998e9e75a0aea0ba3fa8f3a9511e9c67f3e0c227694db27a822f62d9a67d` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB035/README.md` | `d1a7dec94a0bd6c5a8893705609415352c18c86c474170910bbaab72540f4776` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB036/README.md` | `bc11588f53802802f642a259dbe8f5fd87c57589f00a994ddb49049516de20df` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB034/manager-visible-readonly-diagnostic-projection-proof.md` | `2a048a2d9d350946961ccd2f8811042fd8abfba57b06ea0f4946094e7bf60362` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB035/no-mutation-redaction-evidence-envelope-proof.md` | `7c31c33bd4bd50fe63533327c0f0887afe7a7075b633ba53488ff7efc81a4e10` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt` | `08e80247b6cd9ecdd37cc1cb3d6bd6b90af6470c549de859382771bd9aa24695` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/transcripts/source-assertions.txt` | `0a9c9429053d7f59398aa6150ab3a9a41bc78cb6d53f496c1a160d6b5c774e10` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/transcripts/no-transient-bundle-path-scan.txt` | `276d667a5833cd7c989453e4602588131070b164e3da12d57c925f422cf7c9e6` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/transcripts/anti-stub-and-runtime-host-drift-scan.txt` | `1c4ef889fd1f213fd729890f6639b59fbc59d439ee10fb87f825e88ae74b4532` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/red-team/mutating-manager-diagnostic-proof-rejected.md` | `eac324b6a706dd1ec37492c905bb12268b99cdfb0e0ad3fe9cee14ee60573056` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/semantic-invariants.md` | `b1bea1e7a00fb017c8afc6ad9bdefbf12ab963a7e9b9436d27f028e8265f22b6` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB036/SB036-manager-diagnostics-no-mutation.trx` | `ba4fb52971d10f318581706358ad01a01f3ebcb3abba7edfcc52aec72fc6e80a` |

## Command Transcripts
- Focused integration run: `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- Source assertions: `bundle://proof/SB036/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB036/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB036/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team mutation proof rejection: `bundle://proof/SB036/red-team/mutating-manager-diagnostic-proof-rejected.md`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Manager diagnostic projection | Read-only verification projection mapper | Process manager diagnostic view model/workspace | Projects supplied verification observations into diagnostics mode only when requested | Rejects anonymous manager attachment and mutation flags |
| Manager evidence envelope | Read-only verification projection mapper | Process manager evidence review | Attaches aggregate envelope only in evidence-envelope mode | Rejects implicit envelope attachment in diagnostics/none mode |
| Runtime evidence read-only observation | Runtime evidence verification adapter | Manager diagnostic projection | Carries audit facts, evidence hashes, and manager-readonly permission mode | Denies mutation/untrusted evidence paths |
| Transcript read-only observation | Transcript verification adapter | Manager diagnostic projection | Carries diagnostics/audit facts with redacted sensitive values | Denies unsafe command and unsupported domain lanes |
| Runtime evidence source snapshot | Runtime evidence source provider | Memory/evidence consumers | Emits source-grounded redacted process/workflow runtime items with hash policy | Rejects stale/scope-mismatched cursors and sensitive payload export |

## Closure
- Shallow-pass trap: A fake pass could show diagnostics text without proving supplied-evidence-only projection, denied mutation lanes, redaction, evidence hashes, or strict driver-consumer boundaries.
- Adversarial negative proof: `bundle://proof/SB036/red-team/mutating-manager-diagnostic-proof-rejected.md`
- Semantic positive proof: `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- Anti-stub audit: `bundle://proof/SB036/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Manager diagnostics are source-backed, read-only, redacted where sensitive payloads are involved, and guarded against runtime-driver host drift.
