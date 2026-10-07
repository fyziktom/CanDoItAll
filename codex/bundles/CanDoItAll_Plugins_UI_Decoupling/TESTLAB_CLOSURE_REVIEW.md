# TestLab corrective closure review

## Decision and provenance

Proceed to Plugins after a bounded S0 confirmation. No new blocking TestLab defect was identified in the inspected correction. This is a source/test review, not a guarantee that every runtime path has been independently executed.

Reviewed branch `components-decoupling`, commit `dd050d5a1489537207e073cac0838f40cde4340f`; compared with `3c579fd1a923ad90f619fe144e6e4c1fe081fa8b`. GitHub reports the head two commits ahead and zero behind. The branch was rechecked at the end of source inspection and still resolved to the reviewed head. See [B01/B02](SOURCES.md).

The compare shows three production implementation files changed: the sandbox workspace, sandbox store, and three-line Unknown notification branch in the module page. Tests and documentation were added/updated, with the historical input package committed separately. The renderer, contracts, production session/owner, persistence, project references and previous S0 shell fix were not changed by this correction. This is a comparison result, not a fresh complete audit of every unchanged file.

## Previous findings

| Finding | Inspected resolution | Proof present in source | Assessment |
| --- | --- | --- | --- |
| R1: party selection retires the committed save's read-back and leaves the draft Pending | Typed wait kinds separate References from Readback; ChangePartyAsync starts only a reference lane, while actual editor retirement still ends owned reads | Real Home/EditForm/selector regression retains draft/context, newer unblurred title, section, party and child IDs; first commit settles, refresh is read-only, later explicit save updates the same identity | Addressed in source |
| R2: Project None makes a known saved party appear unavailable | Reference existence is separate from project options; exact saved-ID fallback runs when the option is absent | Global selector, omitted project option, unknown ID, missing/failure and delayed stale reference cases | Addressed in source |
| C1: Unknown has a definite save-failed notification title | Explicit Unknown case says `Test plan save outcome unknown`; Error severity and recovery detail remain | Page/session test observes NotificationService and verifies uncertainty/replay protection | Addressed in source |

Sources: [T01–T03](SOURCES.md). Current tests also cover old reference success/error, A → B → A, independent successor submissions and reset with a still-admitted fake write. Do not remove these tests or solve unrelated future bugs by undoing the independent lanes.

The new fake lookup resolves an exact known party, not an arbitrary ID. Deliberately missing and failed lookups remain distinct. No CRM implementation dependency was introduced for this behavior.

## Validation evidence: reported versus observed

The current corrective receipt records the following **77 distinct passing cases**, all with zero failures/skips and matching discovery [T04](SOURCES.md):

| Owning lane | Recorded current cases |
| --- | ---: |
| Lightweight TestLab UI/components | 38 |
| TestLab session unit tests | 29 |
| Notification and reconciliation host tests | 7 |
| TestLab browser tests | 3 |

The earlier 115-case extraction/S0 receipt is historical and must not be added to 77 or represented as a new rerun. The current receipt says the four affected production builds, portability enforcement and documentation checks passed. It also records a one-sample sandbox Razor watch smoke; historical full-Web/sandbox graph counts and prior comparative measurements are explicitly not new measurements.

The reviewer read implementation and test source and the maintained receipt. The reviewer did **not** build or run the application, discover/execute its tests, inspect the ignored local TRX/screenshots, rerun PostgreSQL or measure watch latency. A GitHub Actions query for the current SHA returned `total_count: 0` [B03](SOURCES.md). This does not contradict a local run and is not evidence of a failed CI run.

## What S0 should actually do

Use actual current source/test discovery. At an unchanged checkout, the light namespace filter historically selected 38 cases, session 29, notification/reconciliation 7 and browser 3; these are reference counts, not constants to force onto a changed checkout.

Start with the narrowly relevant R1/R2 regressions in `TestLabSandboxReviewTests`, `TestLabNotificationTests`, and the positive production-session saved-party fallback. Inspect the current browser scenario asserting Pending → Saved for the specific plan. Expand/rerun the current whole bounded TestLab lanes if source drift, fixture changes or a discovered regression requires it. Reuse a verifiable current local result only when its source/configuration/artifacts genuinely match and the repository permits reuse; a prose receipt alone is not executable proof.

If no defect is reproduced, do not invent a TestLab patch: accept this boundary and move to Plugins in the same assignment. Any small real carry-over is fixed before Plugins. Preserve strict distinction between real commits and fake-store commits, and retain the already-fixed shell's disposal behavior. Final combined closure still requires the current mandatory static gates for the new source changes.
