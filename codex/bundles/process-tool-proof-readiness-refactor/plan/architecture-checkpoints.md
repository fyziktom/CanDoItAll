# Architecture Checkpoints

## Before SB01

- Confirm new models live in process contracts or process application, not MAF prompt code.
- Confirm receipt gate will use recorded runtime/MCP receipts, not artifact summaries.
- Confirm persistence/backward compatibility plan for existing assignments with empty scope.

## Before SB02

- Confirm HR readiness uses the same compiled contract as runtime execution.
- Confirm readiness has typed reason codes for missing capability, suppressed tool, missing MCP, missing skill, and missing project access.
- Confirm readiness does not launch providers or MCP servers.

## Before SB03

- Confirm missing proof is represented as a typed diagnostic from the receipt gate.
- Confirm manager fallback does not silently convert missing proof into artifact recovery.
- Confirm driver extension points are narrow and testable.

## Before SB04

- Confirm templates use typed contract fields for proof requirements and concise instruction fragments for behavior.
- Confirm migrated templates preserve existing process semantics.
- Confirm domain-specific image/UI guidance is not in common MAF workspace plugins.

## Closure Checkpoint

- Confirm no new dependency from MAF to process templates or process modules.
- Confirm no new large conditional policy block in existing hotspot files.
- Confirm cache/lifetime choices are documented and tested.
