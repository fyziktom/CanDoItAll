# Bundle status

Prepared: 2026-08-24  
Overall state: `BLOCKED_SB07_TEST_BUDGET_AUTHORITY`

| ID | State | Depends on | Progression owner |
| --- | --- | --- | --- |
| SB00 | `DONE` | none | baseline and architecture decision lock |
| SB01 | `DONE` | SB00 | protocol, identity, access context |
| SB02 | `DONE` | SB01 | persistence and reconciliation model |
| SB03 | `DONE` | SB02 | central catalog API |
| SB04 | `DONE` | SB03 | bounded OpenAI-compatible relay; August 25 named wire-contract revalidation passed |
| SB05 | `DONE` | SB02, SB03, SB04 | source sync and imports |
| SB06 | `DONE` | SB04, SB05 | local runtime projection and hybrid use; August 25 genuine Release downstream revalidation passed 18/16/10 |
| SB07 | `BLOCKED` | SB06 | preserved backend evidence; exhausted Docker lifecycle/image-build budget requires explicit operator authority and amendment |
| SB08 | `LOCKED` | SB07 | desktop management UI |
| SB09 | `LOCKED` | SB08 | component and browser proof |
| SB10 | `LOCKED` | SB09 | operator docs and repeatable E2E tooling |
| SB11 | `LOCKED` | SB10 | OpenAPI export and SharedInfo skills |
| SB12 | `LOCKED` | SB11 | final regression, running stack, closure |

## Operator feedback repair

- 2026-08-27 `SPMETA` — `DONE`: full-model-catalog feedback repaired and governed gate passed;
  see `subbundles/SPMETA-source-metadata-mirroring/full-catalog-repair.md`. Source/client
  catalogs match: 12 OpenAI chat and 3 Ollama choices, exact available prices/private flags.
  Shared unpriced selections save correctly; Simple Chats preserves source model labels.
  Final focused lanes pass 52/52/24/39 (167 executions). Two complete UI runs and independent
  runtime checks pass with ten complete central successes each, including non-default
  agents/chats and image/vision. Final image fullcatalog-20260827-2 runs on 5210/5212.
  Source/hash, anti-stub and architecture gates pass; data retained and 5032 untouched.
  Governed evidence and handoff: `subbundles/SPMETA-source-metadata-mirroring/RESULT.md`.
  Original SB07 and its downstream locks remain unchanged; no live/paid-provider claim.

## Historical state rules

- Only one subbundle may be `READY` or `IN_PROGRESS`.
- `DONE` requires a passing proof manifest and completed handoff.
- `BLOCKED` requires the exact missing authority or external state.
- A failed progression gate leaves downstream work locked.
- Any named reopen trigger may move an earlier subbundle back to `READY_FOR_REVIEW`.

## Restored trust and active blocker

- Boundary recovery handoff: the corrected ProviderManagement ownership and original SB07 continuation constraints are recorded in [BOUNDARY-RECOVERY-HANDOFF.md](BOUNDARY-RECOVERY-HANDOFF.md); the existing Docker budget blocker and required authority remain unchanged.
- Trigger: SB07 repairs changed the SB04 `OpenAI-compatible allowlist or wire contract` and
  `relay adapter/capability registry` invalidation keys by making Responses persistence explicit
  (`store` is canonicalized to `false`) and rejecting operation/model capability mismatches before
  dispatch.
- SB04 result: the entry/closure validators pass; final Unit, Web, and Integration Release builds
  are clean; exact 24/22/12 selections are freshly discovered and pass.
- SB06 result: the historical Debug transcripts remain chronology only; current Unit and
  Integration Release builds are clean, the unchanged frozen 18/16/10 selections were freshly
  listed and passed in genuine Release, and `sb06-revalidate-closure.txt` passes. CP-04 trust is
  restored.
- Active blocker: SB07 has seven failed governed lifecycle attempts and seven application-image
  builds against the unamended whole-bundle 2/2 ceiling. No further Docker lifecycle or image build
  is authorized.
- Missing authority: exactly one replacement SB07 multi-instance lifecycle and one application-
  image build, with durable cumulative 9/9 ceilings and one lane/build still reserved for SB12.
  The authorization does not include a retry, broad, Playwright, stable-aggregate, live-provider,
  or paid-provider lane.
- 2026-08-26 operator authorization: a separate two-instance UI/runtime acceptance lane is now
  authorized, including focused rebuild/revalidation after a proven defect. This removes the
  Docker authority blocker for that requested lane, but it does not yet satisfy SB07's distinct
  three-application gate; SB07 remains `BLOCKED` until evidence proves that contract or the
  bundle is explicitly amended.
- 2026-08-26 two-instance UI/runtime acceptance: completed and repeated successfully. The shared
  instance published Ollama chat, OpenAI chat, and OpenAI image providers through the UI; the
  provider-empty client imported and used them for chat, image generation, and image analysis;
  the central usage ledger recorded all eight repeat-run operations as successful. Evidence:
  `evidence/two-instance-ui-acceptance/README.md`. This separate lane does not change SB07's
  `BLOCKED` state or unlock downstream subbundles.
