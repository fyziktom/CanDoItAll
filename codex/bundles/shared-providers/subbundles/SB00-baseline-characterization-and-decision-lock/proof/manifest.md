# SB00 governed proof manifest

Subbundle: `SB00 — Baseline characterization and decision lock`  
Proof tier: `Governed`  
Evidence status: `PASS — portable evidence complete; machine status/progression is owned separately`  
Captured: `2026-08-24`  
Product behavior changed: **No**  
Portable invariant contract:
[semantic-invariants.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/semantic-invariants.md)

This document does not replace or mutate `proof/proof-manifest.json`, `STATUS.md`, or
`SESSION-HANDOFF.md`. Those closure-state files are intentionally outside this evidence edit.

## Owned requirements and raw notes

SB00 owns current-state proof, not shared-provider feature behavior. Its controlling raw notes are:

- “Trace provider create/edit/delete, commit observer projection, ordinary agent creation, simple
  chat, workflows, health, image generation, and legacy Workspace execution.”
- “Characterize existing provider usage observation/persistence and whether external relay traffic
  can be represented truthfully.”
- “Characterize provider reference/deletion checks and transaction conventions.”
- “Confirm which connector manifests are production-configurable, including Azure status.”
- “Confirm OpenAI SDK base URI, Responses, Chat Completions, Images, streaming, and custom endpoint
  behavior with narrow characterization tests where source inspection is insufficient.”
- “No shared-provider production behavior.”

Source: [SB00 execution contract](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/README.md).

## Baseline

| Fact | Evidence |
| --- | --- |
| CanDoItAll branch/commit | `providers-shared` at `e46f81d5ee33627dccb548732725e1c37e980ab5`; [identity transcript](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-identity-main.txt) |
| CanDoItAll prepared tree/current runtime | Prepared tree plus .NET `10.0.303`, Docker/Compose, Python, PowerShell, and sibling presence; [runtime baseline](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-repository-runtime-baseline.txt) |
| Formal SB00 execution entry | The repository was clean before readiness repair; SB00 execution began with the repair/proof-scaffolding delta already present, as shown by [entry worktree state](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-state-main.txt). |
| SharedInfo identity | `main` at `053f8b356fbc8a28bf822e0a051c25804bd81b65`; [corrected identity transcript](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-identity-sharedinfo-corrected.txt) |
| Entry gate | Prepared validator passed with zero warnings; [validator transcript](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-entry-prepared-validator.txt) |
| Architecture before/after | 11 scoped projects, 23 direct product references, zero project cycles before and after; [before](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/codeanalytics-before.md), [after](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/codeanalytics-after.md) |

## Changed-file manifest and SHA-256

The complete Git-derived inventory is
[changed-files.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/changed-files.md).
It records before/after SHA-256 values for every tracked modification and every untracked file in
the worktree, including readiness repair, architecture 00–04/07/12, reviews, traceability,
status/report, scripts, handoff, proof JSON, all proof scaffolding, and both tests. Because the
repository was clean before readiness repair, this is the complete current delta against `HEAD`.
The inventory file and the recursively derived bundle/proof hash manifests self-exclude to avoid
a hash cycle; those two manifests validate the final after-state separately.

The table below highlights key SB00 files. No production source or skill file changed in SB00.
`absent` means the file did not exist at `HEAD`; all hashes are lowercase SHA-256 over the file
bytes.

| Class | Portable path | Before | After |
| --- | --- | --- | --- |
| Test | `repo://tests/Unit/CanDoItAll.Tests.Unit/SharedProviderArchitectureCharacterizationTests.cs` | `absent` | `bb149bf1dde6019a7940b135e044dc4f0ec320aa7dac44f1778b09941e0b78ea` |
| Test | `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderRuntimePathCharacterizationTests.cs` | `absent` | `f46cd0999a7a47483f5cae52b3ee85f6bca66b71e6f7802e1733fa13fde3c342` |
| Bundle | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/README.md` | `64bd637f8bf2081d924a3cead440c52d3f6b98b426f2941fbaeebd0249d88c70` | `049ea26d4fe841911370df4fab6b1e80426d3396f4c7a35cd356bb69aa8694e5` |
| Bundle | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/test-selection.json` | `1c3c7c10ac2b73f3d6289c3fa52af3fc0eab67f4303f28e854fe79d3569dc8c2` | `36fcaf021bef360ee36f8a189ff787a57866ae6a236059155b16f2284e467cce` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/semantic-invariants.md` | `absent` | `0b5147a738854121b9b4cb013af13e7e7ec1f29f9f60c9aebdc75c2252699456` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/canonical-persistence-and-secrets.md` | `absent` | `97ea21d14cc640e9377c52cab9812335b4f49c11ab7cc706ef57e2cb4c5e3267` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/connector-capability-inventory.md` | `absent` | `318623138299e916df392c512a2372591e4b13302bf5a13b622d0453b198b7c1` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/assumption-resolution.md` | `absent` | `06f6787d66a91f8718fc7365c02e14bb941d8c5c25959ce2c45fa5b8c838a6f8` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/project-references-before.md` | `absent` | `b3cbc85ca69ad2e20241f7033d1b0ebc54cf7ffb6a9cf8f2817eb9bb24cd26c9` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/codeanalytics-before.md` | `absent` | `7c889e09a2503b8062395396b51063de6e73d23b1542c1c525f302cae5a186d3` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/codeanalytics-after.md` | `absent` | `f5adf8e77df6065e63cfa59eb9fc36dcceefba2679e9a111e6a5d3a27b12e95f` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/api-openapi-sse-compose.md` | `absent` | `b1356ccf590e617f2ffa808f433acf4bc9c1c15a7b2356ea4e8480e8ed1c95b1` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/standards-revalidation.md` | `absent` | `34dd1d6b94766b689a297dcf6896ee4120305fa6f9a1fe2f81e418ab60eeb507` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/runtime-call-paths.md` | `absent` | `117800cb3d3faa7bf05eae9efba326f8e8b50e054534adf27fcaa56c8a314f95` |
| Proof | `bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/usage-and-deletion.md` | `absent` | `6f775c95f42c50250f9249c66a0124e534ac120cb51d0f1ec53353bfbda9f6a2` |

The portable manifest does not self-hash. The owning closure step may add the final manifest hash
to the bundle-wide hash inventory after all closure-state edits stop.

Mandatory skills were read but not changed. Their hashes are recorded in the
[corrected mandatory-skill transcript](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-mandatory-skill-hashes-corrected.txt)
and [SharedInfo skill transcript](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-sharedinfo-skill-hashes.txt).

## Exact build, discovery, and execution proof

| Lane | Build | Exact discovery | Exact execution |
| --- | --- | --- | --- |
| Unit architecture | [successful build: exit 0, 0 warnings, 0 errors](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-unit-escalated.txt) | [8 discovered, exit 0](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-list-unit.txt) | [8 passed, 0 failed, 0 skipped, exit 0](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-unit.txt) |
| Integration runtime | [final SDK transport build: exit 0, 0 warnings, 0 errors](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-integration-sdk-transport.txt) | [6 discovered, exit 0](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-list-integration-sdk-transport.txt) | [6 passed, 0 failed, 0 skipped, exit 0](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration-sdk-transport.txt) |

The commands use the two authorized stable filters from
`bundle://subbundles/SB00-baseline-characterization-and-decision-lock/test-selection.json`.
Zero discovery would fail the gate. No unfiltered project, solution, browser, multi-instance, or
broad regression lane was run.

## Failing-first applicability and preserved failures

Failing-first product proof is **N/A**. SB00 changed no production behavior, so the correct proof
is characterization of the existing system. Creating a product failure solely to obtain a red
transcript would violate the subbundle’s explicit scope. The exact rationale and per-invariant
mapping are in [semantic-invariants.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/semantic-invariants.md).

Real execution failures are preserved and not erased:

| Preserved artifact | Classification | Superseding proof |
| --- | --- | --- |
| [initial unit build](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-unit.txt) | Exit 1 because the sandbox could not read the user NuGet configuration; not a source failure. | Same build executed with the required permission and passed in [unit build](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-unit-escalated.txt). |
| [initial integration build](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-integration.txt) | Exit 1, `CS0104` ambiguous `ProviderKind` in the new characterization test. This is test implementation feedback, not semantic failing-first proof. | The test uses explicit Workspace/AgentFramework aliases; [corrected build](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-integration-corrected.txt) and [6/6 run](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration.txt) pass. |
| [intermediate SDK terminal assertion](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration-sdk-terminal-assertion-failed.txt) | Exit 1 because the test assumed the pinned Responses SDK yields a typed completion update. It actually yields the typed text delta and completes without exposing a separate completion update. | The test now locks the observed behavior; the [final build](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-build-integration-sdk-transport.txt) and [final 6/6 run](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration-sdk-transport.txt) pass. |
| [initial mandatory-skill hash command](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-mandatory-skill-hashes.txt) | Wrong Windows PowerShell host did not provide the expected command; exit 1. | [Corrected `pwsh` transcript](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-mandatory-skill-hashes-corrected.txt). |
| [initial SharedInfo status](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-state-sharedinfo.txt) and [identity](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-identity-sharedinfo.txt) | Git rejected sandbox ownership; both exit 128. | Per-command safe-directory scope passed in [status](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-state-sharedinfo-corrected.txt) and [identity](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-git-identity-sharedinfo-corrected.txt). |
| [initial secret scan](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-secret-scan.txt) | Invalid proof: shell interpolation removed variables, produced command errors, and nevertheless returned exit 0. It is explicitly rejected as a pass. | Correct quoting produced `PASS: no credential-shaped values or private-key blocks found` in the [corrected scan](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-secret-scan-corrected.txt). |
| [initial diff check](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-diff-check.txt) | Exit 2 because the SB00 README state line had trailing whitespace. | Whitespace was corrected and the same command passed in [corrected diff check](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-diff-check-corrected.txt). |

## Artifact-backed semantic proof

| Proof kind | Claim | Durable evidence |
| --- | --- | --- |
| Positive | Canonical Workspace profiles map to effective OpenAI/Azure/ComfyUI runtime profiles. The real pinned OpenAI SDK preserves a custom path for Chat Completions and Responses, normal and streaming; the production image driver preserves the same non-root custom prefix. | [final 6-test integration list](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-list-integration-sdk-transport.txt), [final 6/6 integration run](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration-sdk-transport.txt), [OpenAI SDK transport characterization](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/openai-sdk-transport-characterization.md) |
| Negative | Inner runtime projects have no outer references, the source graph has no project cycle, Web APIs do not expose internal provider request records, Azure has no invented Workspace adapter, and UI does not own EF/HTTP. | [8-test unit list](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-list-unit.txt), [8/8 unit run](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-unit.txt), [reference graph](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/project-references-before.md) |
| Adversarial | Exact connector counts reject silent registry growth; recursive cycle detection rejects indirect loops; distinct connector metadata proves Azure is not inferred from a manifest; custom `/custom/v1` calls reject public-endpoint hard-coding across Chat, Responses, and Images; integrated registration distinguishes normal runtime from legacy fallback. | Both exact [8/8](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-unit.txt) and [final 6/6](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-run-integration-sdk-transport.txt) runs plus [connector inventory](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/connector-capability-inventory.md) |

## Production source assertions

| Claim | Artifact-backed assertion |
| --- | --- |
| Canonical persistence, observers, projections, concurrency, EF registration, transaction convention, and secret lifecycle | [canonical-persistence-and-secrets.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/canonical-persistence-and-secrets.md) contains exact production symbols/line anchors. |
| Agent, Simple Chat, Workflow, health, image generation/analysis, and legacy paths remain distinct typed consumers | [runtime-call-paths.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/runtime-call-paths.md) contains exact call chains and source anchors. |
| Six Workspace connector manifests, Azure metadata status, mapper constraint, and voice/audio limitation | [connector-capability-inventory.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/connector-capability-inventory.md). |
| Deletion lacks a general reference policy; Agent/Simple Chat usage is durable; relay needs a truthful workload/source rather than a second ledger | [usage-and-deletion.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/usage-and-deletion.md). |
| API authorization/error/SSE/OpenAPI and Compose boundaries | [api-openapi-sse-compose.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/api-openapi-sse-compose.md) and [standards revalidation](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/standards-revalidation.md). |

## Assumption classification

The full row-by-row classification is
[assumption-resolution.md](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/assumption-resolution.md).

| Status | Resolution |
| --- | --- |
| `Confirmed` | Workspace EF is canonical; AgentFramework catalog is a projection; committed changes feed the fail-closed runtime snapshot; the six manifest registry and additive usage projection extension point exist; the project graph remains acyclic. |
| `Amended` | There are two write paths; deletion has no general reference policy; editor optimistic concurrency is weak; Azure is production-configurable through OpenAI connector metadata but has no Workspace manifest; unknown connector metadata cannot bypass the mapper switch; provider-secret binding is not uniformly strict; relay needs a dedicated truthful workload/consumer; multi-row reconciliation needs an explicit transaction; image chat is analysis rather than generation; voice currently selects enabled OpenAI chat profiles. |
| `Blocked` | None in the SB00 persistence/runtime/API/architecture evidence lanes. Any later contradiction is a reopen trigger, not residual-risk wording. |

## Anti-stub, secret, browser, and downstream proof

- Dedicated anti-stub command passed for both selected test files:
  [sb00-anti-stub-audit.txt](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-anti-stub-audit.txt).
- Anti-stub applicability rationale: no production file was added or changed, so no new production `TODO`,
  `NotImplemented`, template-only output, fixture branch, or manual seeded production signal can be
  hidden by this subbundle. The only fake types are `FixedHttpClientFactory`,
  `FixedCredentialResolver`, and `CapturingHandler` inside the integration test; they form a
  deterministic transport boundary and are not compiled into production. The positive assertion
  calls the real `OpenAiProviderDriver`. The changed-file table and passing builds are the durable
  audit for this no-product-change scope.
- Secret/content scan: the initial transcript is rejected; the corrected scan passed at
  [sb00-secret-scan-corrected.txt](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-secret-scan-corrected.txt).
- Browser/screenshot/host proof: `N/A`; SB00 added no route, UI, public API, or host-visible
  behavior, and its test budget explicitly disallows Playwright/multi-instance work.
- Critical-foundation downstream proof: the integration lane exercises the current outer
  Workspace-to-runtime mapper, real connector manifests, normal-host gateway replacement, and real
  OpenAI image driver. This proves the selected boundary is consumable by later SB02/SB04/SB06
  work without adding an inner dependency. Downstream remains locked to the dependency plan; this
  manifest does not advance `STATUS.md`.
- Red-team verifier: not a final-closure subbundle. SB12 must re-read this manifest and reject fake
  or missing proof before aggregate closure.
- The reusable subbundle closure validator passed:
  [sb00-closure-validator.txt](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/transcripts/sb00-closure-validator.txt).
  The C# architecture checkpoint is `PASS_SB00` in
  `bundle://reviews/csharp-architecture-gate.md`. The whole-bundle completed-stage validator
  remains correctly deferred to SB12.

## Production behavior artifact matrix

SB00 creates no production signal, state, record, or event. It relies on these existing lifecycle
artifacts, with no test manually seeding a production-only signal:

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Workspace `ProviderProfile` | Canonical save paths in [persistence evidence](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/architecture/canonical-persistence-and-secrets.md) | EF snapshot loader and mapper in the same artifact | Post-commit observer/projection/delete ordering in the same artifact | Canonical-master unit invariant and deletion-gap evidence |
| Runtime descriptor/lease | Canonical snapshot/profile projection in [runtime paths](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/runtime-call-paths.md) | Agent, Simple Chat, Workflow, health, and image typed consumers there | Prepared credential scope plus fail-closed snapshot lifecycle | Integrated-gateway and custom-prefix tests; legacy is not a fallback after runtime failure |
| Provider usage contribution | Agent/Simple Chat writers in [usage evidence](bundle://subbundles/SB00-baseline-characterization-and-decision-lock/proof/behavior/usage-and-deletion.md) | Common `ProviderUsageQueryService` and additive projection sources there | Append-only Simple Chat and file-backed Agent persistence there | Missing usage is not zero; external relay cannot be mislabeled Agent/SimpleChat |

## Gate and reopen decision

The governed SB00 evidence gate passes: all **8** architecture tests and all **6** runtime tests
were discovered exactly and passed, the source graph has no project cycle, production behavior was
not changed, and every prepared assumption is Confirmed or Amended with none Blocked. This evidence
supports progression only as defined by the bundle dependency graph.

Reopen SB00 if any provider project/reference edge, connector registration, Workspace-to-runtime
mapping, canonical persistence rule, secret lifecycle, usage projection contract, API convention,
or selected test count changes.
