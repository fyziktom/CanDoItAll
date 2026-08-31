# Execution progress

Execution authorized 2026-08-30. Entry HEAD `bb154a0ac` adds the reviewed bundle to product HEAD `3fc10d2db7ba7e4e15bc94f50e66f815f31c4219`. Development baseline: `1625b336e4f60ddb64987240c3a3dc485591d20f`. No merge, push, commit, live profile migration or paid upstream call has been performed.

## Working state

- SB01–04: completed. Final combined owning Integration: 179/179, including relay/SDK/network/capture/persistence/source projection/catalog/schema/migration owners. Owning Unit: 145/145. Real recorder late-retry expiry and distinct-revision behavior also pass.
- SB05: completed constant allowlist caching, owned-memory response parsing and cheap persisted catalog stamps. Public usage-extractor span compatibility is retained; byte arrays use memory without a full copy. Cache invalidation/memory lifetime Unit: 110/110. Identical ten-case baseline/after workloads pass. Final six-case selection adds catalog SQL EXPLAIN, concurrent history and both upgrade lanes. Maintenance capacity scenarios and limits are in 03-performance-capacity.md.
- SB06: implemented Web-owned scalar/enum and strict operation schemas; exactly five shared-provider operations remain. Five semantic tests pass. A real Draft 2020-12 validator (jsonschema 4.25.1, installed only in ignored artifacts) agrees with runtime on 28 accepted/rejected payload cases. A failing scalar test exposed arrays missing custom-enum item schemas; fixed explicitly.
- SB07: completed six project READMEs, two product guides and maintained navigation/API/architecture/pricing/security/backup/migration guidance. Final documentation validation passes all 197 maintained Markdown files. Historical PREMERGE-REPAIR-HANDOFF.md transfers documentation/export ownership without rewriting old outcomes or authority.
- SB08: source skills and database proof complete; final export/installation blocked. Four source skills validate and the exact five-package installer preview passes. SharedInfo validation has one expected stale-snapshot workflow-route mismatch, so no current-contract pass is claimed. Both exact-development and populated reviewed-head rehearsals pass. EF reports no pending model changes; full/development/reviewed-head SQL generated, with the latter empty except BOM. EF tool 10.0.3 warns about runtime 10.0.4 but succeeds. Final capture script rejects the unrelated running host; canonical export, support README/manifest and active synchronization remain open.
- SB09: nine direct affected builds and product/Stable Release graphs pass with zero warnings/errors. Isolated desktop browser scenario passes with seven inspected screenshots at 1920×1080. Current nine-project CodeAnalytics/source audit and governed evidence manifests are recorded. The single frozen Stable invocation passed 9,424/9,424 with zero skips/failures (Components1,190, Integration1,237, AgentFramework Memory22, Memory196, Unit6,779). The 9,369 discovery display entries expand through seven source-verified MemberData methods; all 55 additional rows are reconciled in sb09-stable-results.json. Independent execution review and the original distinct three-application authority/proof remain open.

## Validation environment

.NET SDK 10.0.303 / Release / local sibling source references / xUnit VSTest. Builds use `--artifacts-path ./artifacts/premerge /m:1` because the user's existing Web process locks normal Release binaries. That process was not stopped. PostgreSQL tests explicitly select the repository's local fixture connection; only UUID disposable databases are created/dropped, preventing an implicit Docker fallback. No general Docker lifecycle authority is inferred.

## Repair details

Relay buffered Responses requires completed status with no non-null error. Stream failure after headers aborts the transport; pinned SDK regressions wait until the first delta is consumed before the fake upstream fails. Status and Retry-After survive oversized/unreadable optional diagnostics.

Imported runtime client selection shares the source URI policy: HTTP loopback remains local development behavior without granting general private-network access. Redaction now covers quoted credential names and escaped/quoted values. Explicit timeout classification survives the sanitized MAF boundary via a typed timeout flag, without retaining private exception text.

Retention deletes expired empty orphan input rows within the existing batch/partition lock. A bounded per-invocation/revision deadline prevents late retries from recreating expired input after tombstone removal; response expiry remains per attempt. See architecture/05-retention-input-expiry-decision.md. No database model change was introduced.

## Measured allocations

Exact samples, elapsed distributions, SQL text and orphan plan are in the paired TRX files and `sb05-measurements.json`. These local results are workload-specific and include ordinary process noise.

| Workload | Before bytes/request | After bytes/request |
| --- | ---: | ---: |
| Normalization, 1 message | 5,208 | 4,656 |
| Normalization, 32 messages | 40,617 | 25,681 |
| Normalization, 256 messages | 277,051 | 158,174 |
| Buffered relay, about 1 MiB | 8,405,856 | 6,317,497 |
| Buffered relay, about 64 MiB | 577,166,888 | 416,266,497 |
| Cache hit, 1 publication × 32 models | 161,279 | 135,782 |
| Cache hit, 50 publications × 32 models | 1,623,708 | 185,210 |
| Cache hit, 200 publications × 32 models | 6,125,516 | 352,555 |

Catalog hits still perform two queries (service identity and persisted version/secret stamp). Expensive profile/model materialization happens on cache miss. The cache is stored under the version of the rows actually loaded, preserving correctness if data changes between stamp and full read.

## Proof limits

Existing logs include intentional failing-first tests and fixture/setup iteration failures; they are not all passing proof. Corrected SDK failing-first selection is `sb01-sdk-before.trx` (8/8 fail before abort). Sanitized timeout boundary baseline is `sb03-boundary-before.trx` (two shared cases fail, two unsanitized controls pass). Current passing results are explicitly identified above. No final merge readiness is claimed.
