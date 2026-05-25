# Structured Input

## Raw Notes

| Raw note | Summary | Owning subbundles |
| --- | --- | --- |
| N001 | Agents can miss artifacts, produce malformed artifacts, or stall process steps. | SB04, SB05, SB07, SB08, SB09, SB10 |
| N002 | Processes must stay generic for any process type, not just software delivery. | SB01-SB10 |
| N003 | Workflows are not Processes; workflows execute roles while Processes own lifecycle, artifacts, transitions, recovery, and governance. | SB06, SB07, SB08, SB10 |
| N004 | Avoid unnecessary blocks and retries while still preventing scope drift such as architecture work doing implementation. | SB01, SB02, SB08, SB09, SB10 |
| N005 | Remaining source findings require typed contracts, trusted grounding, storage-backed validation, lineage identity, output mappings, invariant audits, and typed recovery. | SB01-SB10 |

## Requirement Map

- RQ01: persisted step operation contracts.
- RQ02: operation-aware tool policy.
- RQ03: trusted grounding ledger.
- RQ04: storage-backed artifact validation.
- RQ05: stable artifact projection identity.
- RQ06: workflow/subprocess output mapping.
- RQ07: recovery continuation.
- RQ08: runtime invariant audit.
- RQ09: typed blocked/failed lifecycle.
- RQ10: generic red-team coverage.
