# P02D-VERIFY

Source: owner findings 2–11 and verification topics 1–24.
Expected: Unconfirmed -> canonical Committed / DefinitelyNotCommitted / StillUnconfirmed -> reconciliation or controlled same-identity retry. No read replays writes. Proposal precedes persistence; committed binding follows proof. Preserve later edits/context/section; failed/canceled/stale/wrong-target reads never unlock. Old operations never clear newer state; callbacks delivered at most once per attempt.

Disallowed: blocks-replay-only proof; arbitrary list success clearing Boolean; name/list identity; fake returned outcome proving commit; another New ID after unknown.

Red: original red-unit, red-components, red-retry-race, red-refresh-lock, red-section TRX. Green: owning-Unit, owning-components-final, recovery-api-final and local-followup TRX. Topic mapping: plan/test-map.json. Source hashes/assertions: file-manifest.json/source-audit.json. Adversarial review: architecture-review.md. Downstream: owning Providers-01/02, not catalog implementation.

| Artifact | Producer | Consumer | Lifetime | Negative proof |
|---|---|---|---|---|
| Local attempt | submission/registry, real DB faults | canonical verifier/operations/API | scoped target/attempt | candidate red, failed read, intervening revision, retry conflict |
| Shared descriptor | management effect | authoritative Retry | target cancellation/generation plus scope | locked Retry red, stale A, rejected warning |
| Source attempt/change | application create/effects | exact canonical verification/parent callback | stable ID, overlay-independent scope | recreation, one row, mismatch, failure/disposal, duplicate callback |

Permanent public/import audit identities remain permanent; backend/API reference-specific tests protect remediation.
