# SB07 Governed Proof Manifest

Date: 2026-07-12. Closure decision: `Pass`.

## Scope And Provenance

- Production scope is the typed access context, bounded opaque-handle registry, authorization coordinator, browser activation authorizer, independent content/save adapters, revision-aware filesystem replacement, thin HTTP context/policy adapters, and hardened content/download/legacy routes.
- The implementation consumes exact `CanDoItAll.FileTools.Abstractions/0.1.0` and `CanDoItAll.FileTools.FileInteraction.Core/0.1.0` packages. No FileTools provider or UI package is referenced by the authority boundary.
- `source-hashes.sha256` records every SB06/SB07 integration source plus the affected Infrastructure, Web, unit-test, and host-test owners from the final verified source state.

## Evidence Index

| Evidence | Purpose | Result |
| --- | --- | --- |
| `semantic-invariants.md` | Named authority, endpoint, re-resolution, independence, mutation, and redaction invariants | Pass |
| `transcripts/failing-first-unsigned-endpoints.txt` | Characterization that unsigned reference/path routes were authority before hardening | Correctly exposed the bypass and defined the required 401/410 outcomes |
| `transcripts/passing-unit-redteam.txt` | Forged/cross-context/stale/revoked/expired, zero-browser, save, and filesystem revision tests | Pass: 30 tests, 0 failed |
| `transcripts/passing-http-host.txt` | Real ASP.NET host behavior for authorized and unsigned routes | Pass: 8 tests, 0 failed |
| `transcripts/source-architecture-audit.txt` | Build, format, dependency, source, performance, logging, and CodeAnalytics gate | Pass |
| CodeAnalytics `snap-20260713042852-baab347b` | SB07 closure dependency/complexity review | Pass: no affected large-file finding or new cycle |
| SB08 re-entry `snap-20260713051010-baab347b` and affected rerun | Current shared-boundary regression after cache/revision integration | Pass: 39 unit and 8 host tests; SB07 hashes refreshed below |

## Authority And Effect Matrix

| Effect | Authority producer | Independent enforcement | Negative proof |
| --- | --- | --- | --- |
| Browse activation | `StorageFileBrowserItemAuthorizer` re-browses the current container occurrence and asks `FileAccessAuthorizationCoordinator` for a grant | Handle binds actor, session, runtime, profile, authorization revision, semantic scope, source, exact operation, and current revision | Forged locator, semantic-root escape, source removal, revision change, and cross-context tests fail before content storage calls |
| View/download | `FileAccessAuthorizationCoordinator` issues exact-operation opaque handles | `AuthorizedFileContentSource` and `AuthorizedFileHttpContentService` resolve and reauthorize every read | Forged, expired, revoked, cross-actor/runtime/session, and wrong-operation handles are rejected; unsigned legacy tokens receive 401 |
| Edit/overwrite | Coordinator requires Edit and requires explicit Overwrite in addition to Edit | `AuthorizedFileSaveTarget` reauthorizes and calls revision-aware storage; filesystem checks expected revision both before staging and immediately before commit | Conflict/failure/cancellation preserve dirty state and revision; overwrite without permission performs zero writes |
| Direct known file | Authorized handle is supplied to `AuthorizedFileToolsKnownFileSessionFactory` | Factory creates FileInteraction content/save contracts directly | Instrumented test proves no FileBrowser catalog, session, provider, browse, or search dependency exists |

## Handle And Lifecycle Decisions

- Handle identifiers are 32 cryptographic random bytes encoded as 43-character base64url values. They are transported only in `X-CanDoItAll-File-Handle`, never in query strings or display DTOs.
- Registry capacity is bounded and deterministic under pressure. Expired entries are removed; otherwise the oldest creation/sequence entry is evicted. Revocation and actor-wide revocation are explicit.
- Records are process-local authority, not durable locators. A context, policy revision, source, runtime, profile, session, operation, or content revision mismatch invalidates use.
- The default binding provider fails explicitly until a module owns bindings; it does not silently invent an empty or permissive scope.

## Architecture And Performance Decisions

- Integration.Abstractions owns typed contracts; Integration owns policies/adapters; Infrastructure owns native storage effects; Web only adapts the current HTTP context and routes.
- No partial class, service locator, sync-over-async, provider SDK leak, reverse Infrastructure edge, raw-handle URL, or authorization decision from display path/client capability was added.
- The largest affected security owner is 278 lines. The final snapshot has no large-file finding in either FileTools integration project or the changed Web adapters.
- Registry work is bounded by configured capacity. Save buffering is bounded by the configured 64 MiB content ceiling. Browser mapping arrays are bounded by native page/breadcrumb budgets. The 64 filesystem write locks are fixed process-wide.
- Logs contain operation outcomes and non-sensitive typed state only. The red-team test injects raw handle, actor, path, and content markers and asserts none appear in captured logs.

## Build, Regression, And Known Baseline Failures

- Final focused Release run: 30 passed, 0 failed across authorization, filesystem browse/revision, and package-boundary tests.
- Final host run: 8 passed, 0 failed for managed/authorized endpoint behavior and DI activation.
- Web Release build with warnings as errors: 0 warnings, 0 errors.
- Focused format followed by `--verify-no-changes`: Pass.
- A broader unit run executed 2,545 tests: 2,541 passed and four unrelated existing checks failed (bundle transient-artifact policy rejects architect-provided tracked bundle paths, capability seed expected v9 while current is v10, and two CrmHr EF-model fixture failures). None touches SB07; every affected class was rerun from final source and is green.

## Downstream And Progression

- The authorized known-file session is the required downstream handoff: it remains usable without live browser state and exposes revision-aware save only when granted.
- SB07 closes and unlocks SB08. Any later path/token authority, missing current-context check, cross-scope collision, log disclosure, or revision/overwrite bypass reopens SB07 and all dependent UI proof.
