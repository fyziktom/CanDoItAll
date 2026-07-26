# Remote Agent Package Import

## Status

- `Completed`

## Objective

- Close N001 with a remote-safe, bounded multipart package import that never depends on a
  caller-controlled server path.

## Success Criteria

- External clients upload archive bytes and receive typed import metadata.
- Traversal, symlink, executable, oversize, hash/schema, and secret-material attacks fail
  before catalog mutation.
- Identical idempotent replay returns the original import; changed replay fails.

## Covered Inputs

- N001 / R001.

## Prerequisites

- Prepared bundle and preparation architecture gate.

## Exact Source References

- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Persistence\Packages\ZipAgentPackageService.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration`

## Deliverables

- Multipart endpoint and explicit request/result DTOs.
- Focused archive inspection/import orchestration with bounded options.
- Import mode, expected hash/version, external identity, idempotency, warning, and
  unresolved-prerequisite behavior.
- Security/retry/authorization tests.

## Dependency Impact

- Defines package hash and imported external-identity behavior consumed by SB02 and response
  schemas consumed by SB07/SB08.

## Validation Depth

- Proof tier: `Behavioral`.
- Not a critical architecture foundation; security semantics still block progression.

## Implementation Steps

1. Characterize existing local-path import and package envelope.
2. Extract/reuse archive inspection with explicit limits and blocked-entry rules.
3. Add import mode/idempotency orchestration at the owning catalog boundary.
4. Map a multipart HTTP endpoint with typed response/error metadata.
5. Add realistic positive and adversarial tests.

## Scope Exceptions

- The existing local/admin import route may remain for compatibility if clearly separated.

## Do Not Do

- Do not accept base64 without an explicit smaller bound.
- Do not import provider credentials or infer unresolved provider/capability bindings.
- Do not put archive policy in the endpoint lambda.

## Acceptance Checklist

- [x] package bytes require no server path
- [x] exact archive hash returned
- [x] duplicate replay does not create a second agent
- [x] malicious archive cases fail before mutation
- [x] authorization remains enforced

## Proof Required

- Targeted unit tests for archive inspection/modes.
- Agent API integration tests for upload, replay, conflict, and authorization.
- Affected project build.

## Closure Evidence

- `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore --nologo`
  passed with 0 errors; only the recorded baseline package-advisory warnings remained.
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
  --no-restore --filter "FullyQualifiedName~ZipAgentPackageServiceTests" --nologo`
  passed 6/6.
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
  --no-restore --filter "FullyQualifiedName~AgentPackageImportServiceTests" --nologo`
  passed 3/3.
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
  --no-build --filter "FullyQualifiedName~AgentPackageImportApiIntegrationTests" --nologo`
  passed 2/2 outside the filesystem sandbox required by the existing test-host secret vault.
- Scoped CodeAnalytics snapshot `snap-20260725233309-b2a91453` found no blocking errors
  and no `AgentPackage` dependency cycles. No `.csproj` changed.
- Source assertion: `AgentPackageImportApi` binds multipart transport and delegates to
  `IAgentFrameworkWorkspaceService`; `AgentPackageImportService` owns atomic catalog and
  idempotency policy; `ZipAgentPackageService` owns bounded archive inspection.

## Closure Decision

- Behavioral proof tier: `Pass`.
- N001: `Solved`.
- Downstream progression: SB02 may proceed. Reopen SB01 if SB02 cannot atomically bind the
  imported external identity or if final OpenAPI differs from runtime behavior.

## Browser Validation Logging

- N/A.

## C# Architecture Impact

### Boundary Ownership

- Persistence inspects archives; Core owns catalog/idempotency policy; Web binds multipart.

### Dependency Direction

- Web -> Core contract; Persistence -> Core/Models.

### Pattern Decision

- Strategy for import modes and adapter for multipart transport.

### Testability Contract

- Archive and mode policy are directly instantiated without Web host.

### Partial Class Policy

- No new catalog partial may own the implementation.

### Architecture Proof Required

- Source assertion that endpoint delegates to focused services.

## Progression Gate

- All security negatives and idempotent replay pass; N001 can be marked Solved.

## Reopen Triggers

- Later external-key work reveals non-atomic identity binding, or OpenAPI/runtime response
  differs.
