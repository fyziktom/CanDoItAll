# SB07 Semantic Invariants

## Invariant SB07_INV_001

- Invariant ID: `SB07_INV_001`
- Source raw note: "Prove direct execution coupling reduction, keep behavior parity, and review source sizes after movement."
- Expected behavior: Dispatcher direct `workspaceService.*` usage is zero after SB06, while remaining AgentFramework workspace usage is limited to the facade and explicitly out-of-scope services.
- Disallowed shallow implementation: Counting only `ExecuteRunAsync` or ignoring direct detail/list/provider calls that still keep dispatcher partials coupled to AgentFramework workspace services.
- Failing-first test: `bundle://proof/SB06/transcripts/dispatcher-direct-call-baseline.failing-first.txt` and `bundle://proof/SB05/transcripts/dispatcher-direct-call-baseline-after-sb05.txt` show 26 direct dispatcher workspace-service calls before migration.
- Passing test: `bundle://proof/SB07/transcripts/coupling-reduction-scan.txt`; `bundle://proof/SB07/transcripts/gate-b-unit-architecture-provider-tests.txt`.
- Changed source files: Gate B adds proof only; the coupling reduction source files are hashed in `bundle://proof/SB06/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB07/source-assertions/gate-b-coupling-review.md`.
- Red-team negative case: Any dispatcher `workspaceService.` call would fail `Dispatcher_execution_path_uses_process_owned_execution_client_after_migration` and show up in the Gate B coupling scan.
- Downstream dependency check: SB08 may start only because Gate B confirms the facade migration reduced dispatcher coupling without starting Process Core.

## Invariant SB07_INV_002

- Invariant ID: `SB07_INV_002`
- Source raw note: "Run process provider parity and receipt smoke tests" and "do not reintroduce MAF product-tool dependencies."
- Expected behavior: Process runtime tools keep exact name parity and access behavior, MAF composition remains provider-owned, and execution receipt projection still materializes provider-native browser tool receipts.
- Disallowed shallow implementation: Treating coupling reduction as complete while process tool names drift, approval/access behavior weakens, or receipts disappear from execution detail projection.
- Failing-first test: N/A - SB07 is a refactor checkpoint after the SB06 production movement.
- Passing test: `bundle://proof/SB07/transcripts/gate-b-unit-architecture-provider-tests.txt`; `bundle://proof/SB07/transcripts/gate-b-integration-provider-receipt-tests.txt`.
- Changed source files: Gate B adds proof only; no provider or receipt source files changed.
- Production assertions: `bundle://proof/SB07/source-assertions/gate-b-coupling-review.md`.
- Red-team negative case: Renaming a process tool, weakening the purpose/access matrix, or dropping projected browser receipts would fail the Gate B test set.
- Downstream dependency check: SB08 can rely on current process tool and receipt behavior while adding minimal contracts.

## Invariant SB07_INV_003

- Invariant ID: `SB07_INV_003`
- Source raw note: "Run deeper refactor reviews after SB03, SB07, and SB10" and "Do not run small, medium, or mobile UI validation."
- Expected behavior: Gate B records source-size and remaining-coupling findings, keeps Process Core/driver packs out of scope, and leaves browser validation as N/A.
- Disallowed shallow implementation: Starting a broad extraction, adding driver packs, or producing unrelated viewport proof during a service/source review checkpoint.
- Failing-first test: N/A - SB07 is a review checkpoint and does not change production behavior.
- Passing test: `bundle://proof/SB07/transcripts/dispatcher-partial-line-counts.txt`; `bundle://proof/SB07/transcripts/no-core-driver-project-scan.txt`; `bundle://proof/SB07/transcripts/maf-and-large-screen-policy-scans.txt`.
- Changed source files: Gate B adds proof only.
- Production assertions: `bundle://proof/SB07/source-assertions/gate-b-coupling-review.md`.
- Red-team negative case: A Process Core project, driver-pack project, or forbidden viewport artifact path would fail the Gate B scans.
- Downstream dependency check: SB08 starts from a documented remaining-coupling list rather than an implicit broad refactor.
