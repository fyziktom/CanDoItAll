# Current State

## Verified improvements

- MAF core/OpenAI/Workflows packages were upgraded to `1.6.2`.
- A2A package was upgraded to `1.6.2-preview.260521.1`.
- Process artifact validation was extracted into `ProcessCompletionArtifactValidator`.
- Artifact content reading is storage-backed.
- Artifact validation can now distinguish `ContentUnavailable` and `ContentHashMismatch`.
- `RecordArtifactAsync` attempts to compute content hashes for managed artifacts.
- Live-run profile and process templates were improved.

## Remaining concern

The code still appears to use the pre-upgrade adapter shape:

- `ChatClientAgentOptions`
- `AsAIAgent`
- custom finalizer tool capture
- custom tool tracing
- custom context contribution trace collection
- custom handoff workflow factory

This is not automatically wrong. A compatibility adapter is useful. But the upgrade must now be converted into deliberate 1.6 adoption:

- message injection for finalizer/guardrail instructions,
- session file support for durable artifacts,
- workflow evaluation expected outputs for process step tests,
- A2A v1 behavior checks,
- OpenTelemetry wrapper compatibility,
- skills discovery/frontmatter compatibility,
- stronger tool approval/middleware integration.
