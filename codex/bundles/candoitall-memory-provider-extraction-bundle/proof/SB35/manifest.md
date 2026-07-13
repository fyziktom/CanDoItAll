# SB35 Proof Manifest

## Revisions

- Main repository baseline: `7c57e10572086f705a48a8ef24457a3ebfb4705a`.
- External Cognitive Memory baseline: `6fdaa2f67a57583076b406b578bf9c86dfb36dbf`.
- Inventory date: `2026-07-12`.
- Semantic contract: bundle://proof/SB35/semantic-invariants.md.

## Artifact Index

- Failing-first characterization: bundle://proof/SB36/transcripts/failing-first-evidence.txt.
- Passing terminal confirmation: bundle://proof/SB40/transcripts/terminal-validation.txt.
- Anti-stub and architecture audit: bundle://proof/SB40/transcripts/source-and-architecture-audit.txt.
- Characterization SHA-256 anchor: d3fde9ceaabecec3766205ffda309f4a598514f3ce365594cef25966cb9103c8.

## Changed characterization tests

| File | SHA-256 after characterization edit | Purpose |
| --- | --- | --- |
| `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryProviderRegistryTests.cs` | `d3fde9ceaabecec3766205ffda309f4a598514f3ce365594cef25966cb9103c8` | Proves deny-fallback currently dispatches an unassigned provider. |
| `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryOperationHandlerTests.cs` | `8a3f891dc1623e040dc05ddd2335fa802f8e7ca20f59c0495572127725714b9c` | Proves a foreign requester can currently read and cancel an operation. |

## Failing-first result

Command:

```text
dotnet test tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj --no-restore --filter "FullyQualifiedName~PR008_Deny_implicit_fallback|FullyQualifiedName~Operation_status_and_cancellation_reject" -v minimal
```

Exit code: `1`.

Semantic failures:

- `PR008_Deny_implicit_fallback_never_selects_an_unassigned_compatible_provider`: expected dispatch denied, actual dispatch allowed.
- `Operation_status_and_cancellation_reject_a_different_requester`: expected status not completed, actual status completed; cancellation reached the current unauthorized path.

Both tests execute the production registry/handler through the real Application/Persistence service registrations. They do not use hand-built handler results.

## Positive baseline

- Main memory suite before characterization: 98 passed, 2 existing composition architecture failures.
- Focused Agent Framework memory/MAF suite: 45 passed.
- External Cognitive Memory suite: 28 passed.

These counts are characterization baselines, not current closure proof.

## Architecture and validator proof

- CodeAnalytics snapshots: `snap-20260712092446-ac8ce1a7`, `snap-20260712092631-7f3b2a52`, `snap-20260712092333-cdf936e2`.
- Required architecture artifacts: `bundle://architecture/00-csharp-current-state-inventory.md` through `bundle://architecture/04-csharp-testability-plan.md`.
- Gate: `bundle://reviews/csharp-architecture-gate.md` records implementation readiness PASS and closure FAIL.
- Prepared validator command completed with exit code `0`: `Bundle is valid for stage 'prepared'`.
- Components MCP transport was unavailable and remains an explicit SB40 validation gap.

## Production behavior artifact matrix

| Behavior | Current artifact | Expected repair owner |
| --- | --- | --- |
| Deny fallback | Failing `PR008` production registry test | SB36 |
| Operation owner access | Failing foreign-requester production handler test | SB36 |
| Native composition removal | Existing `CP001`/`CP002` failures | SB40, with implementation in SB36/SB38 composition work |
| Modes, aliases, directives, multi-provider | Source inventory proves absence | SB37 |
| Typed project context and lossless transport configuration | Source inventory proves context/extension loss | SB38 |
| External auth/project/access isolation | Hosted/source inventory proves absence | SB39 |

## Gate result

PASS FOR IMPLEMENTATION. The final closure failure recorded at this checkpoint was resolved and independently rechecked by SB40.
