# SB09 Integration Backbone Behavioral Review

## Decision

- Checkpoint B: `Pass`.
- UI unlock: `Pass`; SB10 may enter.
- No product repair was justified during this review. The implemented backbone already satisfies the declared boundaries, and introducing another facade, policy layer, or composition abstraction would add code without changing behavior.

## Raw Input And Shipped Behavior

- Covered notes: N003-N009 and N013-N017; requirements R008-R016 and R026-R040.
- The shipped pre-UI backbone is the native bounded Storage browse contract, the outer FileTools adapter, typed semantic browse/known-file requests, current-context opaque authorization handles, independent content/save effects, hardened HTTP routes, bounded process-local listing cache, and after-persistence catalog revision publication.
- Browser keys, display paths, legacy storage reference tokens, and URLs remain descriptive only. Effects require a server-side handle and current access context.

## Boundary And Dependency Review

- Fresh CodeAnalytics snapshot: `snap-20260713052405-baab347b`.
- Scope: Infrastructure, Integration.Abstractions, Integration, Composition, and Web; 5 projects, 137 documents, 426 types, 2,586 members, and 54 service registrations.
- Project graph is exactly:
  - Composition -> Integration, Infrastructure;
  - Integration -> Integration.Abstractions, Infrastructure;
  - Web -> Composition, Integration, Infrastructure;
  - Integration.Abstractions and Infrastructure have no reverse project edge.
- The only cycle is the accepted pre-existing Infrastructure Persistence/ControlPlane module cycle.
- Integration.Abstractions resolves only FileTools Abstractions and FileInteraction Core. Integration additionally resolves HybridCache 10.0.0. No FileTools or HybridCache type/package appears in Infrastructure, modules, or UI.
- `RuntimeHostServiceCollectionExtensions` gained one `using` and two declarative registration calls. It owns no FileTools behavior and no service location.
- No new partial type, service-locator injection, `BuildServiceProvider`, sync-over-async, `Task.Run` I/O wrapper, TODO/FIXME/stub, or known-file FileBrowser dependency exists in the production slice.
- The three `GetRequiredService` uses in Integration are DI alias registrations for one metrics instance and one revision service instance; runtime/domain types do not receive or query `IServiceProvider`.

## Pattern And Security Review

- PSR-02 remains a real adapter boundary: Infrastructure models stay native and one `StorageFileBrowserMapping` owner maps native items, metadata, completeness, and safe typed errors.
- PSR-03 remains a real decorator/test seam: Disabled mode bypasses the store, cached values are bounded raw listing facts, authority uses the uncached native driver, and keys bind runtime, source set, semantic scope, storage fingerprint, query, cursor, and revisions.
- PSR-04 remains a real authority boundary: 256-bit random handles bind actor, session, runtime profile/generation, authorization revision, semantic scope, storage occurrence, operation flags, expiry, revocation generation, and expected content revision.
- Legacy preview/download routes use the same handle header and API authorization as the new routes. Unsigned reference query tokens return 401; direct managed paths return 410.
- No raw handle, actor, path, locator, token, content, or secret is logged. Handle and actor diagnostics use short SHA-256 identities.

## Behavioral And Scale Proof

- FileTools restore/build warnings-as-errors/format/test: Pass; all 440 tests passed after the SB10 bounded-search package re-entry.
- FileTools pack/validate: Pass after prepending the provisioned user-local 10.0.301 SDK to `PATH`; 7 packages plus 7 symbol packages validated.
- Provenance: 14/14 repacked hashes and 7/7 main-repository `ExternalPackages` hashes match the SB10-reentry `proof/SB01/package-hashes.sha256`.
- Main Web Release build with `-warnaserror`: Pass, zero warnings and zero errors.
- Main affected unit filter: 79/79 passed. It includes native contracts, filesystem/IPFS/FTP providers, real 100,000-entry filesystem page-one bounds, Storage/FileTools budget/order/completeness mapping, declarative composition, handle red-team, zero-browser known-file content, cache/revision behavior, Storage JSON bounds, placement, and catalog revisions.
- HTTP host filter: 8/8 passed. Authorized content opens under the current runtime context; unsigned preview/download references reject; direct managed paths stay gone; API-authenticated content requires authentication.
- Positive downstream: `SB07_KnownFileContent_OpensWithoutAnyBrowserDependency` opens expected bytes through FileInteraction contracts and proves no `IStorageBrowseDriver` is present in the interaction scope.
- Aggregate revision downstream: `SB08_SuccessfulSemanticRevisionSelectsNewListing` publishes a semantic revision after success and selects a new listing key; failed/cancelled mutations do not publish.
- Shallow-pass trap: returned page size was not accepted as scale proof. The 100,000-entry test asserts bounded inspections/continuation behavior, and adapter tests assert all native work-budget dimensions survive translation.
- Meaningful negative: cached stale listing activation re-resolves through the uncached driver and cannot mint authority; forged/cross-context/wrong-operation/revoked/expired handles fail before storage effects.

## Package And Static Assets

- Non-UI packages (Abstractions, Browser Core, Interaction Core, filesystem provider) contain zero static web assets.
- FileBrowser.Components contains one scoped CSS asset; FileInteraction.Components contains scoped CSS plus `FileObjectView.razor.js`; FileInteraction.Markdown contains scoped CSS.
- These component packages are not referenced before SB10, and the current Web `staticwebassets.build.json` contains zero FileTools entries. SB10 must add only selected packages and verify their resolved host assets.

## Formatting Baseline

- Focused `dotnet format --verify-no-changes` for Integration, affected Infrastructure Storage/DI, Web routes/context/API, and affected tests: Pass.
- FileTools solution format: Pass.
- A deliberately broader command also included the pre-existing 1,000+ line `RuntimeHostServiceCollectionExtensions.cs` and reported whitespace violations beginning at line 249. The bundle diff for that document is only line 4 and lines 68-69; all reported violations are outside the diff. Reformatting the unrelated owner was rejected as an out-of-scope large rewrite.

## Tool Readiness

- Components: the long-lived MCP connector closed twice. The installed Components MCP was then invoked as a fresh standard JSON-RPC server with `proof/components-sb09-input.jsonl`; both `components_libraries_list` and the concrete desktop project-files `components_recommend` call completed with `IsError=False` and exit code 0.
- Managed watch: workspace bridge `Healthy`; SourceWatch, SourceRun, BuildTest, PublishedCandidate, PublishedActive, and ExternalExecutable lanes supported; atomic runtime and rollback available.
- Playwright: persistent browser server responds and has an active `about:blank` tab ready for the SB10 managed app URL.

## Reopen Conditions

- Any SB10 contradiction in package selection/static assets, native budget translation, semantic authorization, content-handle independence, cache/revision isolation, or declarative composition reopens its owning SB06-SB08 phase and SB09.
- Any failure to use the Components catalog, managed watch, and persistent Playwright loop during SB10 blocks UI proof.
