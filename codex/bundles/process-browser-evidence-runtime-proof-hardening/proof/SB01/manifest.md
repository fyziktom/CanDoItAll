# SB01 Proof Manifest

## Status

- `Required during execution`

## Required Artifacts

| Artifact | Required path or rule | Status |
| --- | --- | --- |
| Failing-first transcript | `proof/SB01/evidence/failing-first-provider-native-browser-evidence.txt` | Pending |
| Passing transcript | `proof/SB01/evidence/passing-provider-native-browser-evidence.txt` | Pending |
| Changed-file hashes | `proof/SB01/evidence/changed-file-hashes.txt` | Pending |
| Source assertions | `proof/SB01/evidence/source-assertions.txt` | Pending |
| Anti-stub audit | `proof/SB01/evidence/anti-stub-audit.txt` | Pending |
| Process artifact record assertion | `proof/SB01/evidence/process-browser-artifact-records.txt` | Pending |

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle proof required | Negative-test citation |
| --- | --- | --- | --- | --- |
| Browser proof artifact record | Production artifact projection from provider-native MCP output discovery | Process validation and artifact views | Test must exercise production projection, not manually seed rows | Missing managed screenshot must fail |
| Browser proof conformance observation | Production validation path when required evidence cannot be imported | Process run diagnostics | Test must exercise real validation path | Detached `.playwright-mcp` reference must produce observation or repair |

## Completion Rule

This manifest is complete only when every path above exists and the execution report cites it. Do not mark `SB01` closed from prose-only proof.
