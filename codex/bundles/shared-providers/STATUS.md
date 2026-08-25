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

## State rules

- Only one subbundle may be `READY` or `IN_PROGRESS`.
- `DONE` requires a passing proof manifest and completed handoff.
- `BLOCKED` requires the exact missing authority or external state.
- A failed progression gate leaves downstream work locked.
- Any named reopen trigger may move an earlier subbundle back to `READY_FOR_REVIEW`.

## Restored trust and active blocker

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
