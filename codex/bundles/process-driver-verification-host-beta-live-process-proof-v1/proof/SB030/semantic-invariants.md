# SB030 Semantic Invariants

## SB030_INV_001 Typed Diagnostics Readback DTO
- Source raw note: REQ-011 requires manager-visible UI/API smoke for verification host diagnostics.
- Expected behavior: the manager facade exposes `VerifyForReadbackAsync`, returning a typed `ProcessManagerReadOnlyVerificationReadbackDto` with status, lane, process run, step run, caller context, projection metadata, diagnostics, audit records, audit record id, and mutation-denial flags.
- Disallowed shallow implementation: DTO-only source with no facade method, no diagnostics/audit lifecycle test, or a UI-only label that cannot be consumed as a typed readback contract.
- Positive proof: `Process_manager_verification_readback_SB028_INV_001_exposes_diagnostics_dto_and_audit_records` in `bundle://proof/SB028/transcripts/manager-readback-focused-tests.txt`.
- Source proof: `bundle://proof/SB028/transcripts/manager-readback-dto-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs` SHA256 `817f912142a95d66590aac253cea5738da00998c2ee459fde5e893974ef99f94`.
- Red-team negative case: `bundle://proof/SB030/transcripts/red-team-manager-diagnostics-shallow-proof-rejection.txt`.
- Downstream dependency check: P19 operator smoke can render or call the DTO without reaching into host internals.

## SB030_INV_002 API-Smoke Diagnostics Projection
- Source raw note: SB029 requires a large-screen or API smoke for diagnostics projection.
- Expected behavior: the readback DTO serializes as a web JSON payload containing `diagnostics`, `auditRecords`, `noMutationPerformed`, and false process/transition/finalizer mutation permissions.
- Disallowed shallow implementation: serialization-only smoke that does not assert diagnostics, audit records, or mutation-denial flags.
- Positive proof: `Process_manager_verification_readback_api_smoke_SB029_INV_001_serializes_diagnostics_projection_without_mutation_permissions` in `bundle://proof/SB029/transcripts/manager-readback-api-smoke-focused-tests.txt`.
- Source proof: `bundle://proof/SB029/transcripts/manager-readback-api-smoke-source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB030/transcripts/gate-j-source-diff-and-anti-stub-audit.txt`.
- Changed source: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` SHA256 `e64a1c5db863a8b32d4cd262fbb3ab8cb3a3e9c7c7c25d5b1729960cb7a26a88`.
- Red-team negative case: `bundle://proof/SB030/transcripts/red-team-manager-diagnostics-shallow-proof-rejection.txt`.
- Downstream dependency check: UI route proof remains optional until a subbundle explicitly touches UI; this API-smoke proof is the selected P10 path.

## SB030_INV_003 Facade And Durable Audit Boundary
- Source raw note: manager diagnostics readback must not weaken the verification host no-mutation or durable audit guarantees.
- Expected behavior: readback is produced by the manager facade, calls verification through the structured host API, and fetches audit records through the facade audit query path.
- Disallowed shallow implementation: direct EF entity exposure in UI, private in-memory readback, or calls into process mutation services for diagnostics.
- Positive proof: `bundle://proof/SB028/transcripts/manager-readback-focused-tests.txt` verifies audit record id linkage and observation hash shape.
- Source proof: `bundle://proof/SB030/transcripts/gate-j-source-diff-and-anti-stub-audit.txt`.
- Red-team negative case: `bundle://proof/SB030/transcripts/red-team-manager-diagnostics-shallow-proof-rejection.txt`.
- Downstream dependency check: scheduler/readiness and operator-smoke phases must preserve the facade/readback boundary.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessManagerReadOnlyVerificationReadbackDto` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs` | API-smoke JSON test consumes the DTO | Focused suite passes 29 tests | Gate J red-team rejects DTO-only proof |
| Diagnostics readback JSON payload | `bundle://proof/SB029/transcripts/manager-readback-api-smoke-source-assertions.txt` | `System.Text.Json` smoke asserts diagnostics and auditRecords properties | `bundle://proof/SB029/transcripts/manager-readback-api-smoke-focused-tests.txt` | Red-team rejects serialization-only proof |
| Durable audit readback records | Facade readback calls `ListAuditAsync` | DTO maps `ProcessVerificationAuditRecord` into audit record DTOs | Focused DTO test asserts audit id linkage and hash shape | Gate J anti-stub audit rejects private readback stubs |

## Gate Result
Gate J is semantically adequate for P10. The manager diagnostics readback API exposes a typed DTO and JSON-smokeable diagnostics/audit payload without process, transition, or finalizer mutation permissions.
