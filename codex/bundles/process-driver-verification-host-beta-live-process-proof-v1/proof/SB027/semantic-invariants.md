# SB027 Semantic Invariants

## SB027_INV_001 Stable Async Manager Facade
- Source raw note: REQ-010 requires a manager-readonly API/service facade without process mutation.
- Expected behavior: manager callers consume `IProcessManagerReadOnlyVerificationFacade.VerifyAsync`, receiving a typed result that preserves structured host success or denial without turning expected denials into API exceptions.
- Disallowed shallow implementation: only keeping the synchronous `Run` command wrapper or a success-only facade that throws for host policy denials.
- Positive proof: `Process_manager_readonly_verification_facade_SB025_INV_001_returns_structured_success_and_audit_query_without_mutation` in `bundle://proof/SB025/transcripts/manager-facade-focused-tests.txt`.
- Source proof: `bundle://proof/SB025/transcripts/manager-facade-contract-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs` SHA256 `ca8d71695740c3dc59e1657981deecbb4d5099f161df7551558f42d6c35c4eab`.
- Red-team negative case: `bundle://proof/SB027/transcripts/red-team-manager-facade-shallow-proof-rejection.txt`.
- Downstream dependency check: P10 diagnostics/readback must consume the facade contract rather than the compatibility-only sync command path.

## SB027_INV_002 Requester, Projection, Query, And Denial Guards
- Source raw note: SB026 requires auth/requester/projection guard tests for the manager-readonly facade.
- Expected behavior: verification and audit-query requests require a nonblank requester identity, projection modes are strongly typed and validated, audit query limits are bounded, and host denials remain mutation-free structured results.
- Disallowed shallow implementation: allowing anonymous manager calls, stringly-typed projection modes, unbounded audit queries, or success-path-only tests.
- Positive proof: `Process_manager_readonly_verification_facade_SB026_INV_001_enforces_requester_projection_query_and_denial_guards` in `bundle://proof/SB026/transcripts/manager-facade-guard-focused-tests.txt`.
- Source proof: `bundle://proof/SB026/transcripts/manager-facade-guard-source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB027/transcripts/gate-i-source-diff-and-anti-stub-audit.txt`.
- Changed source: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` SHA256 `31caaefb723f6ecfc8ae575c9c145b018b92ad7d41db058cd13792be1b3f2585`.
- Red-team negative case: `bundle://proof/SB027/transcripts/red-team-manager-facade-shallow-proof-rejection.txt`.
- Downstream dependency check: manager diagnostics UI/API surfaces must preserve these requester and projection guards.

## SB027_INV_003 Durable Audit Readback Boundary
- Source raw note: Gate H introduced durable audit persistence; P09 manager readback must use that boundary.
- Expected behavior: `ListAuditAsync` accepts a typed manager audit-query request, delegates to `IProcessVerificationAuditQueryService`, returns mutation-denial flags, and does not enumerate process runtime state or a private command-local list.
- Disallowed shallow implementation: querying process run state directly for audit readback, using a private in-memory list, or bypassing the durable EF-backed query service.
- Positive proof: `Process_manager_readonly_verification_facade_SB025_INV_001_returns_structured_success_and_audit_query_without_mutation` asserts audit readback through the facade after host verification.
- Source proof: `bundle://proof/SB025/transcripts/manager-facade-contract-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` SHA256 `2d81f1a3db895d907c1f303b33436eb02a3f804019f70b17c60298d16bea991a`.
- Red-team negative case: `bundle://proof/SB027/transcripts/red-team-manager-facade-shallow-proof-rejection.txt`.
- Downstream dependency check: P10 and P19 readback proof should use this facade/query boundary instead of reaching into EF entities directly.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `IProcessManagerReadOnlyVerificationFacade` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` registers the interface | `bundle://proof/SB025/transcripts/manager-facade-focused-tests.txt` | `bundle://proof/SB027/transcripts/red-team-manager-facade-shallow-proof-rejection.txt` |
| `ProcessManagerReadOnlyVerificationFacadeResult` | Production command service maps host success/denial into typed manager results | Integration tests assert success and denied states | Focused suite passes 27 tests | Gate I anti-stub audit rejects placeholder/fake closure |
| `ProcessManagerReadOnlyVerificationAuditQueryRequest` / `ListAuditAsync` | Production facade delegates to `IProcessVerificationAuditQueryService` | Tests assert readback includes the verification audit record | Gate H durable audit proof plus Gate I facade proof cover the lifecycle | Red-team rejects in-memory-only readback |

## Gate Result
Gate I is semantically adequate for P09. The manager-readonly facade now has an async structured API, requester/projection/query guards, durable-audit readback, sanitized audit requester projection input, and mutation-denial flags backed by focused tests and source scans.
