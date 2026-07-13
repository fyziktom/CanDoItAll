# SB39 Proof Manifest

## Status And Scope

- Status: Completed; terminal live-driver proof passed in SB40.
- Requirements: R14-R17, R19, R20, R26, R28, R29.
- Main repository revision anchor: `7c57e10572086f705a48a8ef24457a3ebfb4705a` plus the current uncommitted repair working tree.
- External repository revision anchor: `6fdaa2f67a57583076b406b578bf9c86dfb36dbf` plus the current uncommitted repair working tree.
- Semantic contract: `bundle://proof/SB39/semantic-invariants.md`.

## Artifact Index

- Before/after SHA-256 anchors: `bundle://proof/SB39/transcripts/file-hashes.txt`.
- Local and copied-isolation validation: `bundle://proof/SB39/transcripts/external-local-and-isolated-validation.txt`.
- CodeAnalytics and dependency-boundary audit: `bundle://proof/SB39/transcripts/codeanalytics-and-boundary-audit.txt`.
- Failing-first N/A for this process reconstruction: no full production pre-change executable transcript was preserved and none is fabricated.
- Passing terminal main-driver process proof: `bundle://proof/SB40/transcripts/terminal-validation.txt`.
- Source, partial-class, and anti-stub audit: `bundle://proof/SB39/transcripts/anti-stub-audit.txt`.
- Protocol fixtures owned by the external repository: `docs/protocol/fixtures/context-query.v1.json`, `docs/protocol/fixtures/ingestion.v1.json`, and `docs/protocol/memory-protocol-v1-wire.md`, with hashes in the bundle transcript.
- Browser/screenshots: N/A for SB39's service-only boundary; generic UI evidence is in `bundle://proof/SB40/transcripts/browser-validation.txt`.
- Representative SHA-256 after hash: c57a2b6448e59ac6e19c2c0f5cd04ce3597d75bf1de7defd8b27a7273126e87b.

## Semantic Adequacy

- `SB39-INV-01`: the external service authenticates and authorizes memory requests, rejects invalid project scope, and applies the domain access policy before recall materialization.
- `SB39-INV-02`: the external repository owns a dependency-free Protocol v1 contract and builds/tests without a sibling main checkout.
- `SB39-INV-03`: the external manifest and hosted endpoints expose implemented behavior only; raw HTTP fixture conformance is proven here and the actual main `NativeRemoteMemoryProviderDriver` process run passed in SB40.
- Shallow-pass traps rejected: endpoint-local API-key checks, caller-controlled global fallback, post-materialization record filtering, sibling `ProjectReference` coupling, fake advertised routes, and responsibility-grouping partial classes.
- Proof limitation: no trustworthy standalone pre-repair command transcript survived. The baseline revision and before/after hashes are retained; SB40 added fresh live negative/positive proof instead of reconstructing the missing run.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Authenticated project context | Service authentication/authorization hashes | Application/domain access-policy path | Hosted service security tests in the 59/59 external aggregate | Anonymous, invalid credential, missing/wrong project, and project swapping cases |
| Protocol v1 envelope and HTTP DTOs | Dependency-free Contracts project and fixture hashes | Hosted protocol mapper/API | JSON conformance tests and raw hosted requests | Malformed envelope and unsupported-capability cases |
| Project-scoped recall | Domain access policy and persistence hashes | Protocol response mapper | Policy, persistence, and hosted API tests | Redacted, restricted, foreign-project/session, and unauthorized records excluded before mapping |
| Repository boundary | External solution/project graph | Nine-project isolated build | Isolated 0-warning/0-error build and 59/59 tests | No sibling checkout, cross-root project reference, or main implementation namespace |

## Architecture Evidence

- External CodeAnalytics final snapshot: `snap-20260712132721-cdf936e2`.
- Snapshot result: nine projects, no blocking errors, no project-reference cycle, and Contracts with zero project/package references.
- One same-assembly Persistence module cycle remains documented; it is not a cross-project or cross-repository edge.
- Handwritten production files are at or below 171 lines. The only partial type found is the conventional `Program` WebApplicationFactory seam.

## Closure Decision

PASS. SB40 ran the real main `NativeRemoteMemoryProviderDriver` against the launched external service and completed the remaining full-system gates.
