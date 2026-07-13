# SB04 Semantic Invariants

## Invariants

- Builders must be constructible and testable without `MafAgentRuntime`.
- MCP secret binding and local/hosted tool construction must preserve existing security behavior.

## Shallow-Pass Trap

- Moving the class declaration while still passing `MafAgentRuntime owner` would look extracted but remain coupled.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Capability builder outputs | Context/skill/tool/MCP builders | Runtime capability composer | Built per enabled capability | Disabled capability and denied secret tests |
