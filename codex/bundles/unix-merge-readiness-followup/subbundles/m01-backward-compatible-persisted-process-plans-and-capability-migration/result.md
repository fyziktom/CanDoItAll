# Subbundle result — M01

## Anchor

- Repository commit before: `386d8beb6038035f89a9a6961ec017d8213879a5` with the accepted M00 working tree
- Repository working-tree fingerprint after: recorded in `proof/M01/manifest.md`
- Components anchor: `8372c1d55f21b349f8e859470b02eeb4421e96ca`
- FileTools anchor: `f31e20d054003348c7557b9634e0838fc5996ae0` plus the three reviewed dirty files
- Authoritative dependency mode: package (`UseLocalCanDoItAllLibraries=false`)
- Host: Windows x64; SDK `10.0.303`; runtime `10.0.11`

## Changed files

- Builder hash-version contract and canonical V1/V2 hashing
- Persistence entity, mapping, store, configuration, and runtime unit-of-work behavior
- PostgreSQL migration, designer, and model snapshot
- Focused unit and PostgreSQL integration tests
- M01 proof and progression records

## Implemented behavior

Persisted plans now carry an independent hash algorithm version and typed execution state. Exact pre-host-capability V1 payloads are verified with the historic hash algorithm, retain their immutable payload and hash, and become typed `NeedsRecompile`. Current V2 plans remain executable only with consistent metadata. Missing versions are accepted as legacy only before the bounded migration timestamp and only when no host-capability seal fields exist; all ambiguous rows fail closed.

The PostgreSQL migration adds nullable classification fields, classifies only proven legacy/current payloads, marks ambiguous rows `Unknown`, and then makes execution state non-null. EF migration execution is transactional; repeated migration, process-context restart, and down-migration preservation are covered by a real PostgreSQL test.

## Failing-first proof

The exact parent payload/hash fixture initially produced generic `InvalidOperationException`; the transcript is `proof/M01/transcripts/failing-first-legacy-plan.txt`. After implementation it produces `ProcessPlanMigrationRequiredException` with V1 and `HostCapabilitiesWereNotSealed` metadata.

## Commands and results

| Command | Exit | Duration | Evidence |
|---|---:|---:|---|
| package-mode unit test project build | 0 | 12.5 s | `proof/M01/transcripts/validation.txt` |
| focused V1/V2/tamper/boundary unit tests | 0 | 3.6 s | 6 passed |
| package-mode integration test project build | 0 | 17.9 s | no warnings/errors |
| focused PostgreSQL migration test | 0 | 6.4 s | 1 passed |
| CodeAnalytics scoped refresh | 0 | 29.3 s | `snap-20260812113133-65c5b773`; no blocking errors/cycles |

## Validation reuse/invalidation

- Invalidated keys: Builder plan hashing; Persistence plan mapping/storage; PostgreSQL migration chain; M08 integrated candidate.
- Reused evidence: M00 anchor/hygiene only.
- Reason reuse is valid: M01 does not change M00 repository-hygiene behavior.

## Security and redaction

Typed exceptions expose only plan identity, algorithm, and remediation reason. Payload content is not logged or included in error messages. Tampered payloads are rejected before migration metadata is persisted.

## Residuals

The scoped architecture analyzer retains a complexity warning for the existing mapper/converter file. No new architecture boundary, partial class, service locator, or project reference was introduced. Source-mode duplicate assembly provenance remains an M02 blocker.

## Decision

`GO`

## Next eligible subbundle

M02
