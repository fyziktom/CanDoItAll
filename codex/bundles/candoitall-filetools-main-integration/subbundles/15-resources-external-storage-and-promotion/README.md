# SB15 Resources External Storage And Promotion

## Status

- `Completed`

The subbundle passed 2026-07-13.

## Objective

- Add an authorized Resources browse catalog over project/filesystem/IPFS/FTP sources and safely promote a selected storage object into a persisted resource.

## Covered Inputs

- N006, N008, N010-N014; R006, R013, R021, R024-R030.

## Prerequisites

- SB14 Completed; SB04 remote providers, SB07 authority, and SB08 cache/revision remain trusted.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceConnectorPlugins.cs`
- `repo://src/Modules/CanDoItAll.Modules.Resources/ResourceSourceSnapshotProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Resources/ResourcesModuleServiceCollectionExtensions.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright`

## Deliverables

- Focused Resources file-source catalog combining authorized project and configured filesystem/IPFS/FTP bindings.
- Registry/Browse UI split using shared components; no confusion with existing memory `ResourceSourceSnapshotProvider`.
- Generic `resource.storage-object` connector (preferred provider-neutral stable binding/object locator) or a documented narrower connector only if the current storage model proves provider-neutral persistence impossible.
- Promotion command re-resolves item/handle and authorizes current actor/operation, then persists stable configuration—not display path/handle/token.
- Successful promotion/source change bumps revision after save; failure/cancel does not.
- Security/persistence/cache/provider/browser proof.

## Dependency Impact

- SB16/SB18 depend on persisted stable object identity and authorization; this is a governed mutation boundary.

## Validation Depth

- Proof tier: `Governed`.
- Security/privacy/persistence boundary; require `bundle://proof/SB15/manifest.md` and semantic invariants.

## Implementation Steps

1. Characterize connector persistence and add failing-first forged/stale/cross-actor promotion tests.
2. Implement source catalog and stable connector model with migration/compatibility as required.
3. Implement promotion application service/transaction and revision publication.
4. Add focused Browse pane and host callbacks using Components MCP.
5. Test each source class with fakes; opt-in remote live smoke supplementary.
6. Prove persistence/reopen, failure rollback, desktop UI, and C# architecture.

## C# Architecture Impact

- Focused catalog/promotion/connector types; page stays rendering/orchestration.

## Boundary Ownership

- Resources owns catalog/promotion persistence; outer integration authorizes/maps; native providers browse.

## Dependency Direction

- Resources -> Projects/Integration/FileTools allowed as planned; no Workbench reverse edge; Integration does not reference Resources.

## Pattern Decision

- Focused catalog/application service; no command hierarchy unless existing module convention requires it.

## Testability Contract

- Catalog/promotion/connector directly tested with fake scope/handle/storage/persistence; page/browser prove wiring.

## Partial Class Policy

- Existing cohesive Razor code-behind allowed; do not add new partial or move business behavior into it.

## Architecture Proof Required

- Connector schema/owner, transaction/revision producer matrix, direct tests, refs/cycles, page responsibility, source/security assertions, C# gate.

## Scope Exceptions

- Remote live systems may be unavailable; fake transport proof mandatory. No broad Resources redesign.

## Do Not Do

- Do not persist handle/display path/unsigned token as authority, reuse memory snapshot provider name, mark success before transaction, or silently omit unsupported provider.

## Acceptance Checklist

- [x] All named authorized source classes appear truthfully.
- [x] Promotion re-resolves/re-authorizes and persists stable connector identity.
- [x] Reopen resolves the persisted object correctly.
- [x] Forged/stale/cross-actor/failure/cancel cases leave no resource/revision.
- [x] Desktop UI, dependency, persistence, and C# gates pass.

## Proof Required

- Governed manifest/hashes/transcripts/invariants, producer-consumer-lifecycle matrix, persistence artifacts, red-team negatives, browser DOM/screenshots/review, anti-stub/source/dependency assertions.

## Browser Validation Logging

- Route Resources page; `1900x1200`, `1440x900`.
- Switch Registry/Browse, select project/filesystem/IPFS/FTP sources, browse/search, promote, inspect saved resource, reopen, provoke denied/stale/failure states and menu/dialog overlay.
- Assert provider capability honesty, persistence result, one scroll owner, no clipping, zero unexpected console/page/network errors.

## Progression Gate

- SB16 enters after governed promotion/persistence/red-team/browser/C# proof and a reopened resource reads through current authorization.

## Reopen Triggers

- Unstable persisted locator, handle/token authority, unauthorized promotion, revision-before-save, provider lie, or page business logic reopens SB15 and affected final proof.

## Suggested Agent Prompt

```text
Implement the governed Resources browse and storage-object promotion story only. Persist stable provider-neutral identity after current reauthorization, prove rollback/revision and hostile cases, and keep catalog/promotion logic out of the page.
```
