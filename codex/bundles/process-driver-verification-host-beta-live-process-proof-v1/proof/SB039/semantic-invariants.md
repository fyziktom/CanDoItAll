# SB039 Semantic Invariants

## SB039_INV_001 Process Core Dependency/API Boundary
- Source raw note: REQ-013 requires Process Core to remain generic and dependency-clean.
- Expected behavior: Process Core has no references to process drivers, modules, EF, DI, HTTP, filesystem helpers, verification hosts, or verification gateways; its descriptor families remain explicit and ordinal-stable.
- Disallowed shallow implementation: checking only `.csproj` references, ignoring source-level using drift, or adding driver/gateway dependencies through helper files.
- Positive proof: `bundle://proof/SB037/transcripts/core-dependency-api-snapshot-tests.txt`.
- Source proof: `bundle://proof/SB037/transcripts/core-dependency-source-scan.txt`.
- Red-team negative case: `bundle://proof/SB039/transcripts/red-team-core-contract-governance-shallow-proof-rejection.txt`.
- Downstream dependency check: verification pack, execution-blocking, observability, and release-candidate gates must not weaken Core dependency boundaries.

## SB039_INV_002 Driver Contract Version And Descriptor Governance
- Source raw note: SB038 requires driver contracts/version snapshots before downstream pack and release gates.
- Expected behavior: `ProcessDriverContractVersion.Current` remains `1.10.0`; `ProcessDriverCoreDescriptorFamily` ordinals remain stable; verification gateway lanes are explicit; allowed operations are read-only verification operations.
- Disallowed shallow implementation: documentation-only contract claims, version checks without descriptor ordinal checks, gateway scans that do not assert operation modes, or treating `ExecutionCapableFuture` as an approved permission.
- Positive proof: `bundle://proof/SB038/transcripts/driver-contract-version-snapshot-tests.txt`.
- Source proof: `bundle://proof/SB038/transcripts/driver-contract-version-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB039/transcripts/red-team-core-contract-governance-shallow-proof-rejection.txt`.
- Downstream dependency check: execution-capable driver gates must remain blocked unless a future bundle updates contract versioning and approval state with explicit proof.

## SB039_INV_003 Contract Boundary Has No Placeholder Closure
- Source raw note: Critical gates must not close on report-only or placeholder proof.
- Expected behavior: Gate M has focused unit transcripts, source assertions, direct Core source scans, anti-stub audit, proof index, and semantic invariants.
- Disallowed shallow implementation: report rows marked passed with no transcripts, old fixture docs as the only proof, or anti-stub scans that ignore production source.
- Positive proof: `bundle://proof/SB039/transcripts/gate-m-proof-index.txt`.
- Anti-stub audit: `bundle://proof/SB039/transcripts/gate-m-core-contract-anti-stub-audit.txt`.
- Red-team negative case: `bundle://proof/SB039/transcripts/red-team-core-contract-governance-shallow-proof-rejection.txt`.
- Downstream dependency check: SB040-SB042 pack-boundary work must consume this proof rather than reintroducing discovery or self-registration.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Process Core descriptor families | `ProcessDriverContractApiVerificationBoundaryTests` | Driver abstraction evidence references consume typed families | SB037 focused unit tests | Red-team rejects project-file-only proof |
| Driver contract version | `ProcessDriverContractVersion.Current => 1.10.0` | Contract/version tests and source assertions consume it | SB038 focused unit tests | Red-team rejects docs-only version proof |
| Verification gateway allowed lanes | `ProcessDriverVerificationGatewayLaneRules.AllowedLanes` | Host selector and contract tests consume explicit lanes | Gate M focused transcripts | Red-team rejects execution-capable gateway claims |
| Core/contract source boundary | Core dependency and anti-stub scans | Downstream pack/release gates | Gate M proof index | Anti-stub audit rejects placeholder closure |

## Gate Result
Gate M is semantically adequate for Core/contract governance. Process Core remains dependency-clean, driver contracts remain versioned and read-only, and the verification gateway remains explicit and execution-blocked.
