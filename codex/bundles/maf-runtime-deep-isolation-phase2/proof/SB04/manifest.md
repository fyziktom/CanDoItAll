# SB04 Proof Manifest

## Status

- Planned.

## Required Evidence

- Changed-file hashes.
- Build transcript.
- Builder unit test transcript.
- MCP positive/negative tests.
- Source scan for no private runtime builders and no `MafAgentRuntime owner` constructors.
- Anti-stub audit output.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Capability builder outputs | Context/skill/tool/MCP builders | Runtime capability composer | Built per enabled capability | Disabled capability and denied secret tests |
