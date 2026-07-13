# Assumptions And Risks

## Assumptions

- The main app remains .NET 10 interactive-server Blazor and continues to use the local `ExternalPackages` feed.
- FileTools public contracts at commit `bdfa4a3...` are the integration target; package hash drift requires SB01 revalidation.
- Application authorization can expose an explicit actor/runtime access context. If it cannot, content and save flows fail closed until one exists.
- The first pilot can use authorized project-owned filesystem/managed-file sources and a known Markdown/text file without implementing every user story.
- Missing provider cache settings deserialize to Disabled.
- Sources can contain at least 100,000 direct children; first-page and search behavior must remain within declared budgets.
- Project Structure node-open already identifies one asset. Its replacement dialog can authorize and open that file without discovery.

## Critical Path Risks

- Adding browse methods to `IStorageDriver` would force unsupported operations on all providers and couple existing placement/transfer callers to browsing. Use a browse sidecar contract/registry.
- Direct FileTools references from Infrastructure would reverse the target boundary and make Foundation depend on UI-oriented packages.
- UI could accidentally legitimize existing unsigned tokens or path-existence checks. New effects require opaque server handles and current authorization.
- Project structure, process, and composition hotspots will become less maintainable if implementation adds partials/switch arms instead of focused owners.
- FTP listing metadata is server-dependent and current `FtpWebRequest` is obsolete. The provider must advertise only proven capabilities; unsupported metadata/search must fail explicitly.
- IPFS immutable CID/DAG and mutable MFS are different freshness classes. A user flag cannot declare mutable data immutable.
- FileTools filesystem paging currently enumerates, snapshots, hashes, sorts, and materializes all direct children before `Skip/Take`; adopting it unchanged would make UI paging cosmetic.
- Main IPFS reads currently create an `HttpClient` and buffer the full response per call; browse/content integration could magnify connection churn and file-sized allocations.
- Reusing FileBrowser inside a single-asset preview would add catalog/source/session/list/search state and latency without serving a browsing use case.

## Validation Risks

- External IPFS/FTP systems may not be available in CI. Unit tests with fake transports are mandatory; live tests are opt-in and cannot be the only proof.
- Main Playwright suites contain quarantined artifact tests. New critical flows need non-quarantined deterministic tests or an explicit reason with equivalent managed-browser proof.
- FileTools SDK and Components MCP gaps can prevent trustworthy package/UI work. Do not substitute old transcripts for current execution.
- Screenshots alone cannot prove authorization, correct source replacement, stale-handle rejection, or save revision behavior.
- Wall-clock-only performance tests are noisy. Structural counters (inspected entries, metadata calls, retained items, bytes) are mandatory, with calibrated timing as supporting evidence.

## Reopen Triggers

- A FileTools API/package mismatch reopens SB01/SB06.
- Provider mutation not visible under Disabled policy, inconsistent paging, traversal, secret leakage, or false capabilities reopen SB02-SB04.
- Any new project cycle, inner-to-outer reference, service locator, duplicate registration, or large-owner growth reopens the most recent architecture gate.
- Cross-actor, cross-runtime-profile, stale-revision, or unsigned-token access reopens SB07 and invalidates all UI proof.
- Cache data crossing authorization/runtime/source boundaries or a failed mutation advancing revision reopens SB08.
- Pilot browser proof that bypasses real adapters, opens stale content, or relies on fixture-only branches reopens SB10 and blocks SB12-SB16.
- A later user story exposing a missing generic contract reopens the owning foundation; it must not patch the gap in a page component.
- Page-one work that grows with total directory size, per-call HTTP client/full-response buffering, unbounded retained search state, or a known-file path invoking FileBrowser reopens the earliest owning provider/integration phase.
