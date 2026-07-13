# SB07 Authorized Handles Content Save And Endpoint Hardening

## Status

- `Completed`

Governed proof passed and SB08 was unlocked.

## Objective

- Establish the server-side authority/effect boundary for browse activation, content, download, and save; harden legacy managed-file endpoints so unsigned tokens/paths are never authority.

## Covered Inputs

- N006-N007, N014-N017; R010-R012, R023, R028-R040.

## Prerequisites

- SB06 Completed with trusted package/boundary/composition proof.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs`
- `repo://src/App/CanDoItAll.Web/Program.cs`
- `repo://src/App/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/StorageJson.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll.FileTools\docs\host-integration-security.md`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.Abstractions\FileInteraction\FileInteractionContent.cs`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Core\FileSaveContracts.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs`

## Deliverables

- Typed explicit access context/operation/scope/actor/runtime contracts and current-context adapter.
- Bounded cryptographically random handle registry with expiry/revocation/actor/runtime/source/operation/revision binding.
- Browser item re-resolution/authorization and independent FileInteraction content source/save target.
- Separate typed known-file interaction and collection-browse requests; the direct content adapter has no FileBrowser/session dependency.
- New authorized handle content/download endpoint path as needed.
- Characterize then harden/deprecate existing storage/managed endpoints; unsigned token or path alone cannot read protected content.
- Awaited save reauthorization/revision/overwrite policy; masked actionable logs.

## Dependency Impact

- Every UI effect depends on this security foundation. Any defect invalidates SB08-SB18 browser/effect proof.

## Validation Depth

- Proof tier: `Governed`.
- Security/privacy/mutation boundary; require `bundle://proof/SB07/manifest.md` and semantic invariants.

## Implementation Steps

1. Characterize current authorized/unauthorized endpoint behavior and failing-first forged-token/path cases.
2. Implement explicit access context; no ambient anonymous/default provider behavior.
3. Implement bounded handle registry and authorization coordinator.
4. Implement content/save adapters independent of browser session and prove direct known-file open with instrumented zero-browser-call fakes.
5. Harden endpoint mapping/policies and compatibility path.
6. Add unit/integration/red-team/log-masking tests and DI/endpoint smoke.
7. Run CodeAnalytics/security source assertions and architecture gate.

## C# Architecture Impact

- New security/effect services in outer integration plus thin Web adapters.

## Boundary Ownership

- Modules define semantic scope; outer integration enforces; Web adapts HTTP; Infrastructure executes authorized native operation.

## Dependency Direction

- Web -> Integration; Integration -> Infrastructure/Abstractions; no reverse edge.

## Pattern Decision

- PSR-04 server registry and thin adapters; no bearer display DTO.

## Testability Contract

- Registry/authorization/content/save tested directly with fake context/storage/clock; endpoint tests use host.

## Partial Class Policy

- No partial security service or endpoint monolith.

## Architecture Proof Required

- Scope/effect ownership, handle invariants, dependency result, direct tests, endpoint policy source, no-service-locator and masked-log assertions.

## Scope Exceptions

- Does not implement module UI or cache. Legacy route removal may be deferred only with a secured compatibility adapter and explicit SB16/SB18 decision.

## Do Not Do

- Do not authorize from FileBrowser key, display path, capability, URL, unsigned token, root existence, or client state. Do not log raw handle/path/content/secret.

## Acceptance Checklist

- [x] Forged/expired/revoked/cross-actor/cross-profile/wrong-operation handles fail before storage call.
- [x] Unsigned token/path alone cannot read protected content.
- [x] Authorized content survives browser-session disposal.
- [x] A known authorized file opens through the content source without constructing or invoking FileBrowser state.
- [x] Save conflict/failure/cancel remains dirty and revision unchanged.
- [x] Endpoint/auth/DI/log proof passes.

## Proof Required

- Governed manifest, hashes, failing/passing/red-team transcripts, semantic invariants, endpoint/host artifacts, source assertions, anti-stub audit, downstream authorized pilot handoff smoke.

## Browser Validation Logging

- Host/API proof only; record authorized/unauthorized HTTP requests. User-visible interaction proof is SB10/SB16.

## Progression Gate

- SB08 enters only after the security gate proves every effect is current-context authorized and legacy endpoints cannot bypass it.

## Reopen Triggers

- Any later stale/cross-scope/unsigned/path-based access, unmasked log, or save bypass reopens SB07 and all downstream UI proof.

## Suggested Agent Prompt

```text
Implement the governed authority/effect boundary only. Start with forged and cross-context failures, bind opaque server handles to actor/runtime/scope/operation/revision, harden endpoints, and prove storage is never invoked on denial.
```
