# SB01 Proof Manifest

## Status

- `Not started`

## Required Evidence

- Changed-file hashes for runtime/tool receipt changes.
- Focused test transcript proving:
  - failed `project_structure_asset_create` / `project_structure_node_create` attempts are recorded as failed receipts or governed platform error records,
  - blocked outcomes that claim required-tool failure without any failed receipt remain invalid,
  - recovery/escalation receives failed receipt diagnostics.
- Source assertions citing the exact changed methods.
- Anti-stub audit transcript for changed files.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Required Negative Test |
| --- | --- | --- | --- | --- |
| Failed project-structure tool receipt | MAF project-structure tool wrapper or execution audit writer | Process completion evaluator and recovery packet builder | Created when tool invocation fails; read during completion evaluation and recovery | Blocked outcome with no failed receipt still fails. |
| Safe tool diagnostic | Project-structure runtime gateway/tool wrapper | Agent result, escalation, recovery packet | Exception/code sanitized, persisted, surfaced in run detail | Raw `Function failed` alone is insufficient. |

## Planned Transcript Paths

- `bundle://proof/SB01/transcripts/failing-first.txt`
- `bundle://proof/SB01/transcripts/passing-tests.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
