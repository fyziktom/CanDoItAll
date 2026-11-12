# H1 — account generation and bounded read-only authority snapshots

Status: approved H1 source and 16 Unit Facts authored and frozen for independent review in the managed runtime checkout; no build, test, scan or database operation. Exact source inventory and pending proof are in [the H1 source record](../validation/P02b-H1-authority-file-source.md). H1 prepares existing Infrastructure stores. H2 will compose the HTTP observer/startup/transport/client changes after H1 proof. Neither checkpoint claims an atomic revocation lease across SQL commit.

## Minimum scope and reuse decision

Retain one full account snapshot and the existing validation core **outside P**. Add its generation to the runtime snapshot. Do not build the previously proposed streaming user selector in H1: it would add another parser and duplicate validation solely to optimize existing outside-lock cost. `ReadAsync`, the new snapshot read, readiness migration and writers share one envelope codec and account validator. A bounded `JsonDocument` preflight can check envelope members/array count before invoking the existing `ApiUserRecord` serializer; this is ordinary framework JSON handling, not a custom general parser. The tiny canonical-header reader has only the fixed v2 envelope states and uses `Utf8JsonReader`.

H2 will have `ApiSessionService.ResolveIdentityAsync` and its runtime snapshot variant project from one shared identity-validation result. Middleware and `WebCurrentPrincipalResolver` retain their existing verified-claim, registered-token, account-revision and exact-scope semantics. H1 does not implement another authorization service, change public session JSON, reuse a middleware decision indefinitely or insert an additional full-account read beneath an owner lock.

## On-disk envelope and authority generation

The new canonical account envelope is schema v2 with properties in this order: `schemaVersion`, `authorityGeneration`, `users`. Generation is a nonempty server-minted GUID. The entire document is published by the existing private `DurableFileWriter` atomic replacement under the existing account mutation gate. No sidecar, cache, watcher, new registry or per-user directory is introduced.

Generation rotates for account creation/deletion and changes to Id, UserName, NormalizedUserName, Enabled, AuthenticationRevision, PasswordHash or AllowedScopes. Compare raw scope values ordinally in sorted order, retaining duplicates; do not normalize invalid input into equivalent valid authority or reference Workspace's scope catalog from Infrastructure. PasswordHash is included because the low-level store permits a hash change without a revision increase. Never derive the public GUID from secret data or log the comparison values.

DisplayName, Version, CreatedAtUtc and UpdatedAtUtc alone do not rotate. Existing valid-document checks and account CAS remain unchanged. The current administration service increments AuthenticationRevision even for cosmetic UI updates, so those actions still rotate; this slice does not silently change that session-invalidating policy. Ordinary/failed login does not write accounts. Login rehash changes the hash and rotates. There is no last-seen/failed-login mutation in the actual account owner.

Save/Delete compare against the complete currently held document under the existing gate. Preserve its generation only when authority is unchanged; otherwise mint a new value. Authority A→B→A receives different generations. CAS/validation failure publishes neither new bytes nor a generation. Token files retain their existing schema and mutation semantics; their bounded current reads do not require an account generation.

Freeze incoming `ApiUserRecord.AllowedScopes` once at the SaveAsync boundary, before the first await and before validation, projection comparison or serialization. The externally supplied IReadOnlyList may be mutable; all three operations must use the same captured scope collection. The generation proof is invalid if comparison reads one collection state and publication reads another. Add mutation-after-capture coverage within the authority-field Fact; do not add a second test-only write seam.

## Exact existing-store API additions

`ApiUserStore.cs` owns:

- `ApiUserAuthorityGeneration`: immutable validated nonempty GUID wrapper.
- `ApiUserStoreSnapshot`: that generation plus a detached immutable collection of `ApiUserRecord` values; every nested AllowedScopes collection is also detached/immutable.
- `ApiUserAuthorityObservation`: closed cases `Current`, `Changed`, `Unavailable(ApiAuthorityReadFailure)`.
- `Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default)` on `IApiUserStore`.
- `Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)` on `IApiUserStore`.
- `ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected, CancellationToken cancellationToken = default)` on `IApiUserStore`.

`ApiTokenRegistry.cs` owns:

- `ApiTokenCurrentObservation`: closed cases `Found(ApiTokenRecord record)`, `Absent`, `Unavailable(ApiAuthorityReadFailure)`; Found freezes its scope collection.
- `Task PrepareCurrentReadAsync(CancellationToken cancellationToken = default)` on `IApiTokenRegistry`.
- `ValueTask<ApiTokenCurrentObservation> ObserveCurrentAsync(Guid id, CancellationToken cancellationToken = default)` on `IApiTokenRegistry`.

One shared small failure enum, placed with these existing control-plane contracts, has `NotReady`, `MissingPath`, `UnsafePath`, `InvalidFormat`, `SizeLimit`, `IoFailure`. A typed `ApiAuthorityReadException` carries only that enum for snapshot-read failure; no raw private exception message becomes a caller result. Caller cancellation propagates as cancellation. H2 owns its observation-budget expiry classification, rather than H1 pretending every I/O exception proves a timeout.

The final methods do not collapse failure into false. `Changed` means a **valid supported current header with a different nonempty generation**. Missing/legacy/malformed/duplicate/reordered/oversized header is Unavailable. A missing account document after readiness is Unavailable, never an empty user store. A missing root or token directory is Unavailable; a missing individual token leaf under an existing safe token directory is explicitly Absent, preserving current credential-deletion semantics. Any race that makes path health uncertain is Unavailable. These cases must be tested separately.

ReadSnapshot requires successful v2 readiness and never migrates or provisions. Existing `ReadAsync` retains its supported absent/legacy read behavior for existing administrative callers, implemented through the shared bounded codec; it is not the final observation API. Explicit Save/Delete remain real mutations and always publish canonical v2 after validating any supported legacy input. No hidden migration occurs on an authorization read.

The v1 branch preserves ordinary unique legacy property order/casing and the existing Web serializer's numeric-string handling; it uses the same bounded DOM/account-count preflight before record deserialization. V2 remains strict about its canonical names/order and never falls back to v1 when invalid. Duplicate legacy schema/users members fail explicitly rather than choosing an ambiguous upgrade source.

## Readiness and removal of hidden filesystem writes

There are three existing side effects to exclude from final observations:

1. Each store's `ResolveRoot` calls `EnsureDirectory`.
2. `ControlPlanePathResolver.ResolveRootPath` itself calls `EnsureDirectory` for the control-plane root.
3. `PhysicalFileSystemPathPolicyFactory.Create` detects case sensitivity by creating, flushing and deleting a probe file.

H1 readiness is the only new path that invokes these provisioning/probing operations. It captures the resolved absolute root, exact store directory and actual immutable `IPhysicalFileSystemPathPolicy` on the existing singleton store. At most one cohesive internal helper holds this read-location metadata and shared bounded byte-read mechanics. It contains no authority decision, credential cache or held stream/coordination lease. No new public path-resolver method or read-only path-policy framework is needed.

ObserveAuthority/ObserveCurrent require that prepared location; if absent, return NotReady rather than preparing it lazily. They invoke only the held policy's `EnsureSafePath`, read-only attributes/private-mode verification and bounded file reads. They do not call the resolver, policy factory, `EnsureDirectory`, `HardenPrivateFile`, chmod, a mutation gate or any migration method. Parent/root/directory removal, links or unsafe permissions fail explicitly; final observation never recreates or repairs a path. Root configuration belongs to the existing singleton/host lifetime; changing its root requires readiness of a new host/store instance, not reuse of an old prepared location. Do not cache credentials or generation results.

`EnsureAuthorityFormatAsync` runs outside any Projects/SQL transaction. Under the existing account mutation gate, absent state becomes a canonical empty v2 document; exact supported v1 is fully validated and rewritten once with a new generation; valid v2 retains its generation; unknown/invalid state fails without overwrite. Only successful/read-back-confirmed readiness makes final reads available. A pre-publication failure leaves v1/absence. A failure after atomic publication may leave valid v2; an explicit retry validates and preserves that generation. No retry loops rewrite an already accepted upgrade.

`PrepareCurrentReadAsync` provisions/verifies the existing token directory and captures its policy, then ends; it does not enumerate or alter token records. H1 exposes these readiness operations but does not wire a dormant HTTP observer. H2 adds explicit startup calls before accepting API traffic and mirrors those in `ApiTestHost`; startup errors fail readiness. Older account-store binaries reject schema v2 and cannot safely join the upgraded writer population.

## Bounds and JSON behavior

Authored fixed ceilings: account document 64 MiB, token document 64 KiB, canonical account header 4 KiB, and the existing 10,000-account limit. H1 deliberately omits a new 16 KiB per-account limit and a streaming parser; those were part of the larger optimization alternative. Whole-account reads allocate only outside P and must remain visible in later complete HTTP allocation measurements.

For full reads, open a single handle, check its length before content allocation, read at most the permitted ceiling plus one byte and reject growth/oversize. Parse UTF-8 with existing framework JSON APIs. Validate exact v2 envelope membership/order/uniqueness, schema/generation and account-array count before record materialization; then use the existing record serializer and shared semantic/duplicate-identity validation. Do not allocate an unbounded UTF-16 source string. The token reader reuses its existing schema/record validator with the byte ceiling. Writes validate and enforce serialized byte ceilings before durable publication.

The read byte ceiling is enforced on the stream before string, DOM or record-list construction; the account count is checked from the bounded DOM before the record list is constructed. Writes enforce the byte ceiling after UTF-8 serialization and before durable publication; this is not a pre-allocation writer bound. H1 has no pre-allocation per-record field cap. A single large valid field can still allocate heavily during deserialization, serialization and semantic validation. The 64 MiB ceiling bounds input/publication, not total heap size, and 64 MiB times 32 concurrent writers is explicitly not a scale-ready claim. H2 carries the generation out of the initial resolver's already-required single account read; it must not perform another full snapshot read merely to obtain the generation. Existing middleware and resolver validations remain distinct as they are today, but each shares the same core and produces identity plus generation in one pass.

The prefix reader consumes no more than 4 KiB and accepts only the fixed valid v2 header ending at the `users` array start. It does not scan or materialize user records. Open handles permit the cooperating atomic replacement to publish a new file without the observation accidentally holding a Windows mutation lock. There is no read-time coordination lease. A bounded old/new snapshot is acceptable; no mixed-document generation/account snapshot is.

The generation optimization assumes canonical, valid, cooperating publishers. Full initial validation and each writer reject malformed unrelated accounts and duplicate identities. Prefix-only observation cannot detect malformed or duplicate fields appended later in the body while an external writer preserves the header; H1 tests/documentation must state that limitation rather than imply tamper detection. Unsupported online direct edits/restores require stopped writers and explicit full validation plus fresh-generation publication before use. Missing/corrupt headers never trigger a full-read fallback under P. File timestamps/length are not an authority version.

## H1 affected files, no wider implementation

Production: existing `ControlPlane/ApiUserStore.cs`, `FileApiUserStore.cs`, `ApiTokenRegistry.cs`, `FileApiTokenRegistry.cs`; at most one new cohesive `ApiAuthorityFileRead.cs` for prepared read location/bounded bytes, and an account-envelope codec only if the existing store otherwise becomes unmaintainable. Reuse existing Infrastructure path-policy DI. No Projects, Workspace session, Web startup/transport/client, migration, reference or registration changes in H1.

Existing test implementations requiring compile-coherent additions: `ApiAdministrationOutcomeHttpTests.ReadBackFailureStore`, `ApiUserAdministrationPanelTests.ControlledUserStore`, `ApiTokenAdministrationTests.RecordingTokenRegistry`. These do not currently serve protected final observations: implement delegation where a real file store exists, otherwise explicit unsupported behavior unless the test actually exercises the new contract. No default interface fallback to full reads. Existing direct constructors may need the already-registered path-policy factory argument; keep those fixture adaptations mechanical and report exact affected paths before coding.

The exact constructor adaptation files identified in source are Unit `ApiAdministrationDurabilityTests.cs`, `ApiAdministrationOutcomeTests.cs`, `ApiTokenRegistryTests.cs`, `ApiCredentialRulesTests.cs`, plus Integration `ApiAdministrationOutcomeHttpTests.cs`. The two Components interface fixtures above are the only additional existing files in that mechanical set. No constructor/count change should add unrelated coverage rows.

## Source-first validation plan

Current source: `ApiTokenRegistryTests` 6 Facts; `ApiCredentialRulesTests` 6 Facts. H1 adds one new **16-Fact Unit file** `ApiAuthorityFileSnapshotTests.cs`, no PostgreSQL case or new fixture. The full affected selection has **54 source-derived rows**: 45 Unit, 6 Components and 3 Integration; only the 16 H1 Facts are additions. These are expected discovery/execution counts, not executed results.

| Planned Fact | Meaningful boundary |
| --- | --- |
| V2 snapshot round trip | Detached users/scopes, exact generation and canonical header. |
| Authority field matrix | Each included field rotates; scope permutations are stable, invalid/raw changes do not normalize away. |
| Cosmetic rewrite | Excluded fields preserve generation while ordinary CAS/version rules still hold. |
| Authority ABA | A→B→A and account deletion/recreation cannot reuse a generation. |
| CAS rejection | Stale account expected version leaves exact bytes/generation unchanged. |
| Independent account change | Unrelated authority edit returns Changed without pretending the selected user was denied. |
| V1 upgrade/restart | Retained user data preserved, one generation, restart idempotence. |
| Absent readiness | Explicit preparation creates only the expected private store state; observation alone never provisions. |
| Unsupported/malformed readiness | No overwrite/default-empty behavior. |
| Pre-publication failure | Existing durable writer fault seam leaves original state and final read NotReady. |
| Post-publication lost acknowledgement | Retry recognizes exact complete v2 generation; no repeated rewrite. |
| Observation outcomes | Current, Changed, NotReady, missing path/document, invalid format and absent-token leaf remain distinct. |
| Zero observation writes | Record resolver/policy-factory calls after preparation; no new call/probe during observations. Remove prepared directories and assert they stay absent; inspect owned file set/content/write metadata, not OS access timestamps. Never rely only on a timing-sensitive watcher. |
| Token bounds | Exact/over limit, growth/malformed input, no private details in failure. |
| Account bounds/validation | Document/count limits before materialization; malformed unrelated account and duplicate ID/name refuse full snapshot. |
| Prefix boundary and cancellation | Large body does not become a final full read; malformed tail limitation is explicit; caller cancellation is not Changed/Denied and no handle remains held. |

Use existing owned file environments and supported `DurableFileWriter` fault seams; no production reflection or test-only hooks. Parent later serializes build/discovery/execution and static checks. The authored bounds Fact serializes 10,000 accounts with representative valid field widths and refuses an oversized submitted field before publication. It is not a maximum API-wire proof: that requires the concrete identity/display/scope catalog limits and remains part of H2's complete measurement gate. Case-probe proof uses the real factory behind a recording wrapper; a fresh factory creation would be visible, while safe-path validation remains real.

## H2 handoff and service-failure distinction

H2 must preserve a single session-validation core and verified claims while carrying the pre-admission snapshot generation to the final observer. It maps generation Changed to a typed no-effect conflict; re-admission may keep the exact operation/payload/business expectation. It maps Unavailable to service failure, for example HTTP **503 with a Rejected envelope**, not 403. Commit uncertainty remains **503 with an Unconfirmed envelope**. OpenAPI/generated clients/native parsing must switch on the body discriminator and operation knowledge, never infer commit state from status alone. An unavailable new attempt cannot clear a prior ambiguous attempt.

H1 alone neither invokes this new observer nor closes HTTP authority. Full outside-P validation cost, constant-size final-read cost, unrelated-authority conflict rate, small-read cancellation/OS stalls, real wait/revoke tests and complete HTTP capacity remain H2 gates. Source authority leases and existing business CAS/receipt semantics remain unchanged.
