# Normalized Requirements

| ID | Requirement and acceptance | Owner |
|---|---|---|
| R01 | Reuse known case semantics only within a verified operation interval; reduce redundant probes without weakening fresh path/reparse/root checks, payload durability, permissions or cancellation. | SB01 |
| R02 | Avoid full runtime profile/model materialization for revision probes; preserve existing validated-availability/null semantics, local/shared revisions, supported mutation invalidation and malformed-catalog detection. | SB02 |
| R03 | Immediate existing-run commits may reuse freshly validated plans only under the same uninterrupted locks. Recovery retains full disk validation and exactly-once roll-forward. | SB03 |
| R04 | Retain per-stage awaited progress commits, event ordering, journal shape/version/order, payload flushes and explicit exceptions. No accumulation/batching, fire-and-forget, parallel startup or new cancellation UI. | All |
| R05 | Preserve activity timestamps, agent LastUsedAtUtc, revision metadata, usage totals/history, latest-session summaries and terminal state. Unchanged usage observations alone cannot suppress required writes. | SB03 |
| R06 | Real Playwright MCP conversations, actual tool calls/results, follow-up and history reopen succeed on BOTH 5032 and 5214; UI-negative/tool-error cases are visible and durable. | SB03 integrated gate; SB01/SB02 prerequisites |
| R07 | Measure comparable pre/post startup on each host; include submit-to-run and pre-provider stages, preserve raw samples, require repeatable improvement with no material tail regression. | Phase 0 and integrated gate |
| R08 | Keep changes inside existing owners/projects; no schema/public contract/global factory cache/source-policy changes or unrelated refactor. | All |
| R09 | Preparation changes bundle files only; execution and live mutations are not started. | Preparation |
| R10 | Preserve target host configuration/data/security; no central5210 restart, no broad process kill, no fixture resets or credentials in evidence. | Execution preflight and integrated gate |
