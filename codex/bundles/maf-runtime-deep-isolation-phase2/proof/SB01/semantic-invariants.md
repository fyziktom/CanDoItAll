# SB01 Semantic Invariants

## Invariants

- The inventory must name every current `MafAgentRuntime` partial and private nested runtime-owned type.
- The inventory must assign every listed type to a target owner or explicit exception.

## Shallow-Pass Trap

- A scan that lists only files but omits nested types would falsely pass.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| N/A | N/A | N/A | Inventory only | N/A |
