# SB09 Fake-Proof Resistance

## Checks
- Current-run artifact proof cannot be satisfied by stale lineage; the stale-lineage integration test rejects completion.
- SB08 scenario proof cannot be accepted as prose only; every scenario includes browser state JSON, console JSON, screenshots, run detail JSON, process artifacts, usage summary, genericity audit, and closure receipt.
- Browser proof cannot hide a failed app load; each run has zero console errors, zero icon warnings, CDP assertions, and screenshots.
- Usage proof cannot silently imply cost; unavailable provider usage is explicitly represented with `actualCostUsd: null` and an incomplete-usage explanation.
- Generic app generation cannot rely on hard-coded scenario branches in production/template/skill code; exact scenario-name scan returned no matches outside proof/template scenario packets.

## Negative Replay
- Stale execution lineage replay: passed rejection test.
- Duplicate connector idempotency replay: passed single-command test.
- Background automation race replay: SB08 rerun under `PublishedCandidate` lane produced 35 artifact records, 5 completed runs, 20 completed step records, and zero duplicate artifact-title records per step.

