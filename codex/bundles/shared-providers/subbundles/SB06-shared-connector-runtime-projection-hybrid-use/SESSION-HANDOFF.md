# SB06 session handoff

State: `PASS`

## Outcome

The shared connector now projects a validated source/import/profile graph into the existing
OpenAI-compatible AgentFramework and MAF runtime. Personal and shared providers coexist in one
catalog, selection remains explicit, source-managed models are bound to their publication, and an
unavailable shared selection never falls back to a personal provider. Source-managed profiles are
also excluded from voice selection and rejected at both audio driver entry points before credential
or HTTP dispatch.

All implementation, build, focused behavior, redaction, cancellation, independent security, and
final architecture gates pass. The cumulative changed-file inventory and proof hashes complete the
portable closure package; the governed closure validator is the final progression command.

## Repository state

- branch: `providers-shared`
- commit before/current: `e46f81d5ee33627dccb548732725e1c37e980ab5`
- working tree before: uncommitted completed SB00-SB05 implementation and evidence
- working tree current: uncommitted completed SB00-SB06 implementation/evidence plus preserved
  in-progress SB07 implementation/evidence
- unrelated changes: preserved; no commit, stage, discard, reset, or push

## Architecture evidence

- checkpoint: `PASS_SB06`
- references before: `proof/architecture/project-references-before.md`
- references after: `proof/architecture/project-references-after.md`; its normalized transcript
  comparison reports the same 103 selected `ProjectReference` rows as before and zero delta
- CodeAnalytics before: `snap-20260825070408-300644c7`
- CodeAnalytics after: `snap-20260825100508-300644c7` was force-refreshed after the audio repair
  with 14 projects, 766 documents, 35 modules, 5,281 dependency facts, 34 direct product references,
  zero project cycles, unchanged two module/one nested-type cycles, and zero error findings; summary
  at `proof/architecture/codeanalytics-after.md`
- public/partial review: `proof/architecture/changed-namespace-public-surface-review.md`
- independent review chronology: `proof/architecture/cross-review.md`
- focused architecture characterization: 8/8 passed
- independent architecture re-review: `PASS`, no P1/P2 blockers after audio repair
- independent security re-audit: `PASS`, no P1/P2 blockers

## Build and focused test evidence

| Topic | Expected | Actual | Passed | Failed | Skipped | Artifact |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| runtime profile materializer | 18 | 18 | 18 | 0 | 0 | `proof/transcripts/sb06-revalidate-run-materializer-release.txt` |
| runtime projection integration | 16 | 16 | 16 | 0 | 0 | `proof/transcripts/sb06-revalidate-run-runtime-projection-release.txt` |
| hybrid selection integration | 10 | 10 | 10 | 0 | 0 | `proof/transcripts/sb06-revalidate-run-hybrid-release.txt` |

The current Unit and Integration solution Release builds pass sequentially with zero warnings and
zero errors at the SB04 revalidation artifacts
`../SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-build-unit-release-final.txt`
and
`../SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-build-integration-release-final.txt`.
No product or test source changed before the SB06 Release runs. Original supporting exact lanes
remain Debug-era behavior chronology: architecture 8/8, runtime snapshot 8/8, feature
matrix 16/16, execution preparation 9/9, connector registry 3/3, profile-save validation 30/30,
catalog projection 12/12, concrete drivers 54/54, agent voice 29/29, MAF transport boundary 13/13,
workflow diagnostics 4/4, credential dispatch 10/10, and the SB00 runtime-path invalidation lane
6/6.

The table is current Release authority from the August 25 downstream revalidation. It was required
after SB07 changed named SB04 relay semantics and the earlier SB06 transcripts were found to target
Debug assemblies. All three filters were listed first, retained their frozen counts, and ran with
`-c Release --no-build --no-restore -m:1`. The boundary decision and exact evidence are recorded at
`proof/architecture/sb04-downstream-invalidation-release-revalidation.md`.

## Positive behavior

- The pure materializer validates graph identity, source state, snapshot integrity, cached profile
  integrity, purpose/transport, capability intersection, and source/publication availability.
- The outer mapper projects source-managed profiles as `ProviderKind.OpenAi` with typed source-token
  binding, network policy, shared origin metadata, remote capabilities, and an exact model allow-list.
- Raw OpenAI driver chat-completions, Responses, and image calls and ordinary MAF SDK chat and
  Responses calls reach a deterministic local central endpoint through the hardened named client.
- The same cached named client carries access-context A, then B, then no context without state
  leakage; background execution without an active HTTP context sends no header.
- Personal OpenAI profiles retain unrestricted model override behavior and their default client.
- Personal providers remain available for independently configured voice use.

## Negative behavior

- Missing/corrupt/mismatched source-import-profile graphs do not enter the runtime catalog.
- Missing typed credential, connector origin, model constraint, or valid network policy fails before
  default-client dispatch.
- Foreign/cross-publication routing model IDs fail before raw image or MAF SDK network dispatch.
- Source-managed speech-to-text and text-to-speech fail with a typed safe error before credential
  resolution or HTTP dispatch. Persisted shared voice selections fail closed without choosing the
  first personal provider.
- Source outage, unpublish, retirement, and identity mismatch retain a disabled profile; selecting
  it fails explicitly and never substitutes the available personal provider.
- Requested cancellation remains `OperationCanceledException` and is not reclassified or sanitized
  as a provider failure.

## Security and redaction

Source-managed health and runtime failures are selected by typed
`ProviderCredentialPurpose.SourceAccessToken` metadata and disclosed through deterministic safe
messages. Focused tests prove that private base URI/host/port, secret GUID/value, prompt marker,
and raw network exception text are absent from the returned failure. Audio denial exposes only its
typed safe public message. Module activity writes receive the sanitized message, while personal-
provider diagnostics and audio behavior retain their existing detailed behavior.

## Honest P1 chronology

Final review first identified three real P1 gaps: runtime access-context propagation, publication
model binding, and source-managed failure disclosure. Those were repaired and rerun. A later final
architecture audit then found that shared OpenAI chat profiles could still enter speech-to-text and
text-to-speech consumers. The repair added a typed driver denial before secret/network access,
excluded source-managed profiles from the voice picker, and made an explicitly configured ineligible
shared voice ID resolve to empty rather than a personal fallback. Post-repair builds and exact
18/16/10 plus 16/54/29 supporting lanes pass, and final architecture/security re-review reports no
P1/P2 blocker.

## Closure package

- `proof/changed-files.md` records the cumulative worktree against `HEAD`;
- `proof/hashes.sha256` covers the portable SB06 proof tree;
- the current governed closure validator transcript is `proof/transcripts/sb06-revalidate-closure.txt` before root
  progression advances.

## Reopen triggers observed

The SB04 wire-contract/capability invalidation trigger was observed and closed by the August 25
Release revalidation. The generic connector-neutral seams remain sufficient; no `ProviderKind.Shared`, duplicate
runtime, inner Workspace/SharedProviders reference, or automatic fallback was introduced.

## Progression decision

- result: `PASS`
- next subbundle: `SB07`
- reason: implementation, exact focused proof, changed-file/hash packaging, independent
  architecture/security review, and the governed closure validator pass; SB07 entry validation
  subsequently passes
