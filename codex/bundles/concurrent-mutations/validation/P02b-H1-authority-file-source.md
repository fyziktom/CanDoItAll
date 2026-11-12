# H1 authority file source checkpoint

Status: source prepared, independently reviewed and frozen in `C:/Users/lucys/.codex/worktrees/concurrent-mutations-runtime/CanDoItAll` on 2026-10-16. Parent and independent UI/validation review found no remaining H1 production blocker after the legacy v1 decoder repair. No build, discovery, execution, portability scan, database operation or commit was run. The main checkout and its Stable candidate were untouched. [H1 design](../plan/P02b-H1-authority-file-snapshot.md) remains the scope; HTTP H2 is not activated.

## Exact implementation boundary

Five Infrastructure files under `src/Foundation/CanDoItAll.Infrastructure/ControlPlane`:

- `ApiUserStore.cs`: immutable generation/snapshot, typed current/changed/unavailable outcomes, shared read failures, explicit readiness/snapshot/observation API.
- `FileApiUserStore.cs`: supported v1 decoding and explicit v2 upgrade, canonical v2 writes, authority projection under existing CAS/mutation gate, incoming scopes copied before the first await, full snapshot validation and fixed-header observer.
- `ApiTokenRegistry.cs`: explicit read preparation and detached typed current observation.
- `FileApiTokenRegistry.cs`: existing token schema/semantics, bounded reads/publication, prepared observation and absent-leaf versus unavailable-directory distinction.
- New `ApiAuthorityFileRead.cs`: one cohesive helper holding prepared location/policy, read-only safe path/private-mode checks, bounded UTF-8 stream reads and sanitized failure classification. No authority result is cached.

There are no project references, registrations, migrations, session/HTTP APIs or startup calls in this checkpoint. Existing administrative account reads support absent/v1/v2 state; final observations require explicit readiness. Save/Delete publish v2 only as actual mutations. Older v1-only binaries must not join the upgraded writer population.

Read handles use `FileShare.Read | FileShare.Delete`, allowing the existing atomic replacement publisher on Windows. Final reads never call the path resolver, policy factory/case probe, writer, mutation gate, permission repair or readiness. The stored policy belongs to one resolved host/store root and cannot be silently reused after configuration-root changes.

## Source-derived expected selection

All rows are ordinary Facts or fixed InlineData, with no MemberData/ClassData/Skip in the affected classes. Existing fixture changes add no cases.

| Assembly / class | Facts | InlineData rows | Expected discovery and execution |
| --- | ---: | ---: | ---: |
| Unit / `ApiAuthorityFileSnapshotTests` (new) | 16 | 0 | 16 |
| Unit / `ApiTokenRegistryTests` | 6 | 0 | 6 |
| Unit / `ApiCredentialRulesTests` | 6 | 0 | 6 |
| Unit / `ApiAdministrationDurabilityTests` | 0 | 12 | 12 |
| Unit / `ApiAdministrationOutcomeTests` | 1 | 4 | 5 |
| Components / `ApiTokenAdministrationTests` | 5 | 0 | 5 |
| Components / `ApiUserAdministrationPanelTests` | 1 | 0 | 1 |
| Integration / `Api.ApiAdministrationOutcomeHttpTests` | 1 | 2 | 3 |
| Total | 36 | 18 | 54 |

The seven existing files have only constructor dependency or interface implementation adaptations. Real wrappers delegate new APIs to the real file store; presentation-only fakes throw explicit `NotSupportedException`. No default interface fallback performs a full read under an owner lock.

The new 16 Facts exercise: detached collections and canonical output; each authority field and mutation-after-capture while waiting on the real coordination gate; cosmetic/scope-order preservation plus account CAS; authority ABA/delete/recreate; stale/invalid write exact-byte preservation; unrelated-account generation conflict; legacy upgrade/reopen; explicit absent-state preparation; malformed/unsupported readiness; pre-publication failure; published-but-unacknowledged readiness recovery without republishing; distinct current/changed/missing/invalid/unsafe/I/O/absent results; resolver/factory/write-metadata stability and no directory recreation; exact/over/growing token input and sanitized failure; full-account byte/count limits plus unrelated/duplicate records and retained 10,000-account serialization; and bounded prefix/cancellation/handle release.

The authority field test uses the existing coordination lease to hold Save after its synchronous scope copy, then mutates the caller's list. Fault tests use the existing `DurableFileWriter` stage observer. The growing-stream test exercises the actual internal stream reader, with no new injected production callback or reflection seam.

## Explicit proof limits and remaining gates

Read ceilings are 64 MiB accounts, 64 KiB token and 4 KiB prefix. Length/growth checks happen before JSON/string/record materialization; account-array count is checked in the bounded DOM before records are deserialized. Writer byte ceilings are enforced after serialization and before publication. No per-record allocation ceiling, complete heap bound or throughput claim is made. One bounds Fact deliberately supplies a large account field to prove refusal before publication; its 10,000-account case uses representative valid widths, not maximum API-wire serialization. Maximum concrete API widths/catalog and total allocation remain H2 measurement work. Legacy readiness coverage includes reordered/case-varied unique v1 members and numeric-string schema, while v2 stays strict and never falls back.

Header observation proves the canonical prefix only. The test deliberately retains a valid header with a malformed/oversized external body and observes Current while full reads reject it. This records the cooperating valid-publisher/private-path assumption, not a corruption fallback. Unsupported direct edits/restores require stopped writers, full validation and fresh generation before resume. There is no filesystem-to-SQL atomic revocation lease.

The held policy performs ordinary read-only safe-path checks, not a new adversarial filesystem transaction. Case-probe absence is evidenced by a real factory behind a recording wrapper; write metadata/content/file set and removed-directory assertions are supplementary. Access timestamps are deliberately ignored. The cancellation Fact covers pre-cancelled calls and subsequent handle release; it does not exercise cancellation during a stalled read. Real Windows replacement contention, pending-read cancellation and complete HTTP final-observation costs still require H2 measurement/proof.

Parent validation remains required: direct Infrastructure and affected consumer builds; build-backed discovery against 45/6/3; execution of the same frozen outputs; canonical portability review/enforcement. Independent source review is complete and is not runtime proof. H2 must separately wire explicit startup readiness, share the current principal validation core, preserve claims and current receipt-read disclosure, distinguish unavailable service failure from denial/commit uncertainty, and prove real wait/revoke/write-scope-loss races.
