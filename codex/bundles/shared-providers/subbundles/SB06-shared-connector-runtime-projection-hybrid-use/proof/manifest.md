# SB06 governed proof manifest

State: `PASS`.

## Outcome

SB06 integrates imported shared profiles into the existing Workspace, AgentFramework, and MAF
provider path. A pure outer materializer validates the source/import/profile graph and produces an
effective OpenAI-compatible profile. The outer mapper adds typed source-token, network, feature,
origin, availability, and publication-model constraints. Inner runtime projects remain connector
neutral and use generic HTTP-selection and failure-disclosure seams; no `ProviderKind.Shared` or
second provider runtime was added.

The work ran on `providers-shared` at unchanged commit
`e46f81d5ee33627dccb548732725e1c37e980ab5`. The pre-existing uncommitted SB00-SB05 work was
preserved. No commit, staging, discard, reset, push, or unrelated-file overwrite occurred.

## Architecture

The baseline is `snap-20260825070408-300644c7`. The final force-refreshed snapshot is
`snap-20260825100508-300644c7`: 14 projects, 766 source documents, 35 modules, 5,281 dependency
facts, 34 direct product references, zero project cycles, unchanged governed two module/one
nested-type cycles, and zero error findings. SharedProvider search reports 14 warnings, 50 info
findings, and zero open questions.

The before and after reference transcripts each contain the same 103 selected `ProjectReference`
rows, with no delta. Workspace remains free of a SharedProviders Http edge; inner MAF remains free
of Workspace, SharedProviders Http, Web, and UI edges. Composition owns hardened named-client wiring,
and the outer AgentFramework module owns graph loading, mapping, snapshotting, and catalog projection.

Portable summaries exist at `architecture/project-references-after.md` and
`architecture/codeanalytics-after.md`. Final independent architecture and security re-reviews pass
with no P1/P2 blocker. The final changed-file inventory, hashes, and closure-validator transcript
complete the governed package. `architecture/cross-review.md` records the rejected-then-repaired P1
chronology.

## Authoritative build and frozen proof

| Gate | Result | Artifact |
| --- | --- | --- |
| current Unit solution build | Release; 0 warnings/errors; 23.313 s | `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-build-unit-release-final.txt` |
| current Integration solution build | Release; 0 warnings/errors; 19.660 s | `bundle://subbundles/SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-build-integration-release-final.txt` |
| runtime materializer | Release; 18 discovered; 18 passed | `transcripts/sb06-revalidate-list-materializer-release.txt`; `transcripts/sb06-revalidate-run-materializer-release.txt` |
| runtime projection | Release; 16 discovered; 16 passed | `transcripts/sb06-revalidate-list-runtime-projection-release.txt`; `transcripts/sb06-revalidate-run-runtime-projection-release.txt` |
| hybrid selection | Release; 10 discovered; 10 passed | `transcripts/sb06-revalidate-list-hybrid-release.txt`; `transcripts/sb06-revalidate-run-hybrid-release.txt` |
| governed closure validation | pass | `transcripts/sb06-revalidate-closure.txt` |

The 18/16/10 counts are the frozen SB06 authority. No count was broadened or replaced by an
unfiltered lane.

The August 25 authority above supersedes the earlier Debug test transcripts after SB07 changed the
named SB04 Responses wire contract and relay operation/capability policy. Current Unit and
Integration Release assemblies had been rebuilt cleanly by the SB04 revalidation, and no source or
test file changed before these downstream runs. The boundary review is at
`architecture/sb04-downstream-invalidation-release-revalidation.md`.

## Supporting focused proof

The following original SB06 lanes remain useful behavior chronology, but their transcripts target
Debug assemblies and are not current Release authority. The frozen Release 18/16/10 lanes above
carry the August 25 downstream trust decision.

| Topic | Result | Artifact |
| --- | ---: | --- |
| architecture characterization | 8/8 | `transcripts/sb06-architecture-final-run.txt` |
| runtime snapshot | 8/8 | `transcripts/sb06-snapshot-final-run.txt` |
| feature matrix and voice fail-closed policy | 16/16 | `transcripts/sb06-feature-matrix-post-audio-run.txt` |
| execution preparation | 9/9 | `transcripts/sb06-preparation-final-run.txt` |
| connector registry | 3/3 | `transcripts/sb06-connector-registry-final-run.txt` |
| profile-save validation | 30/30 | `transcripts/sb06-profile-save-final-run.txt` |
| catalog projection | 12/12 | `transcripts/sb06-catalog-projection-final-run.txt` |
| concrete drivers, health disclosure, and audio entry denial | 54/54 | `transcripts/sb06-concrete-driver-post-audio-run.txt` |
| agent voice regression | 29/29 | `transcripts/sb06-agent-voice-post-audio-run.txt` |
| MAF transport failure/cancellation boundary | 13/13 | `transcripts/sb06-transport-boundary-final-run.txt` |
| workflow failure diagnostics | 4/4 | `transcripts/sb06-workflow-diagnostics-final-run.txt` |
| credential dispatch scope | 10/10 | `transcripts/sb06-credential-dispatch-final-run-after-setup-fix.txt` |
| SB00 runtime-path invalidation | 6/6 | `transcripts/sb06-runtime-path-final-run.txt` |

## Behavior and negative proof

The frozen lanes prove persisted graph projection, composite revision invalidation, typed credential
resolution, catalog/snapshot consistency, hardened client selection, actual raw chat-completions,
Responses and image dispatch, actual MAF SDK chat and Responses dispatch, personal-provider
compatibility, unavailable retention, corrupt-graph omission, and absence of inner outer-layer
dependencies.

Publication-owned model IDs are copied into a strict runtime constraint and fingerprint. Foreign
models returned by the central model catalog are filtered, and cross-publication model requests are
rejected before raw image or MAF SDK dispatch. Personal profiles remain unconstrained and retain
their existing override behavior.

Source-managed profiles cannot use speech-to-text or text-to-speech because the imported publication
contract does not advertise those operations. A typed policy blocks both OpenAI driver entry points
before credential resolution or HTTP dispatch. The existing voice selector excludes shared profiles,
and an explicitly persisted shared voice selection resolves to no selection rather than silently
choosing the first personal provider. Independently configured personal voice providers remain valid.

Hybrid proof covers one production registry containing personal and shared profiles, colliding
alias/model text without identity collapse, explicit selection in both directions, outage,
unpublish, retirement, identity mismatch, reappearance, two independent client databases, and
unavailable shared selection without personal fallback.

## Security and cancellation

The same cached named client receives access-context A, then B, then no context. Context is resolved
from the active request scope for each `HttpRequestMessage`, is not stored in default headers, and is
absent for background execution. Existing request headers are preserved.

Typed source-token profiles receive deterministic health/runtime disclosure. Focused proof excludes
private URI/host/port, secret GUID/value, prompt marker, and raw transport details from exposed
failures and workflow diagnostics. Caller-requested cancellation remains an
`OperationCanceledException`; timeouts and transport failures retain their distinct typed behavior.
Audio denial likewise uses deterministic public text without endpoint or provider identity.

## Honest chronology

Earlier transcripts preserve fixture, projection-observer, assertion, and boundary-hardening
failures. They are superseded, not erased. Final authority is restricted to the build/list/run
artifacts named above after the catalog projection, model binding, context propagation, disclosure,
and cancellation repairs.

The subsequent final architecture review found one additional real P1: source-managed OpenAI chat
profiles could still enter speech-to-text/text-to-speech and the voice picker could replace an
explicitly ineligible shared ID with a personal provider. Typed audio denial, voice-option filtering,
and empty/no-fallback selection repaired it. Post-audio builds and exact 18/16/10 plus 16/54/29
focused lanes pass; the final architecture and security re-reviews report no P1/P2 blocker.

## Final packaging

The cumulative `changed-files.md`, `hashes.sha256`, and passing closure-validator transcript form
the portable closure package. Root status advances only after validation. No broad, browser,
multi-instance, live-provider, paid-provider, or Playwright lane ran; no SB08 provider-sharing UI
flow was implemented.
