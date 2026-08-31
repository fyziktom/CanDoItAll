# Desktop UI proof

Result: PASS for the isolated history/policy scenario; no claim about multi-instance inference.

The exact selector FullyQualifiedName~ProviderHistoryPremergeUiTests discovered and executed one case. Passing result: bundle://reviews/test-results/sb09-ui-complete.trx (10 seconds). Release output in artifacts/premerge, .NET 10.0.303, existing Playwright 1.55 Chromium, 1920×1080 / scale 1. The PlaywrightAppFixture started the refreshed product output on a disposable random-loopback host with its own PostgreSQL database and file roots; it disposed that process and database. The test refuses to seed an externally attached host. The running user process on 5032 was untouched.

Earlier sb09-ui*.trx failures are test setup iterations: hidden advanced field, startup modal/hydration, repeated tag-tree representations and tab-label matching. Only sb09-ui-complete.trx is passing closure evidence.

## Inspected images

All seven PNGs under bundle://proof/SB09/screenshots were opened and visually inspected after the passing run:

| Image | Observation |
| --- | --- |
| history-normal.png | Explicit filters/search, applied interval and Partial coverage visible; table is the primary result surface; several rows and Details actions appear in the first viewport |
| provider-history.png | Existing tag-tree/list-detail layout; selected provider identity matches the applied history filter; first result rows visible in the constrained detail pane |
| history-details.png | Wide evidence dialog fits viewport; identity, usage, price, ownership, content action and close controls are readable |
| history-content-light.png | Separate read-only overlay explicitly reports NotCaptured; no prompt/response silently appears |
| policy-light.png | Default policy fields and future/preview actions fit in first viewport |
| policy-detailed.png | Saved policy version and sensitive-text warning visible; future-only change states that existing expiry dates are unchanged |
| policy-retention-preview.png | Compact confirmation layers above policy; 15 standalone rows / zero captured details; cancellation and destructive action readable without clipping |

The shell/content pane remains the vertical scroll owner for long tables; pagination can require scrolling below rows and was exercised both directions. Dialog chrome/actions fit without viewport overflow; the scenario checks overlay bounds numerically. No CSS, component composition, mobile layout or reusable BaseLib component was changed. Existing compact stats and supporting coverage/privacy notices remain subordinate to the table/form.

The test verifies lazy initial history/policy state, 10+5 row paging, details/content separation, per-provider search, Detailed policy persistence and cancellation of a shorter-retention preview. It checks the database still has all 15 fixture rows and original 30/7-day retention after cancellation. This does not claim captured Detailed bodies from a live provider; those are proven by the production capture integration suite.
