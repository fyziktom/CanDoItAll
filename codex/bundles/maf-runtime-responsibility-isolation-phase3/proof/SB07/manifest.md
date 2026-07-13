# SB07 Proof Manifest

## Status

- Planned. Replace planned entries with captured evidence during execution.

## Required Artifacts

| Artifact | Planned path | Required before closure |
| --- | --- | --- |
| DI smoke | `proof/SB07/transcripts/di-smoke.txt` | Services resolve with scope validation. |
| Dependency graph | `proof/SB07/transcripts/dependency-graph.txt` | Before/after refs and CodeAnalytics cycles. |
| Source assertions | `proof/SB07/transcripts/source-assertions.txt` | Service locator, partials, helper/manager names. |
| Build/tests | `proof/SB07/transcripts/passing.txt` | Focused build and tests. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| DI registrations | Composition root | Runtime production path | App/service startup | Resolution and bypass tests. |

## Closure Criteria

- Production wiring uses extracted services.
- Dependency direction is valid.
