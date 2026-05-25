# Bottleneck and risk inventory

| ID | Area | Current status | Risk | Follow-up |
|---|---|---|---|---|
| B1 | SQLite runtime provider | Removed from typed runtime model | Low | Keep residue audit |
| B2 | Hot DB switch/drain | Removed from normal runtime | Low | Keep audit |
| B3 | Process outbox finalization | Conditional finalization implemented | Medium | Add red-team tests |
| B4 | Connector finalization | Conditional finalization implemented | Medium | Add red-team tests |
| B5 | Automation delivery finalization | Conditional finalization implemented | Medium | Add red-team tests |
| B6 | Startup recovery lease release | Still clears non-expired leases | Critical | Fix in SB02 |
| B7 | Long AgentFramework execution heartbeat | Callback-based, may not be continuous | Critical | Fix in SB03 |
| B8 | Process outbox side effects | Idempotency not fully proven | High | Fix/prove in SB04 |
| B9 | PostgreSQL query plans | Not proven | Medium | SB05 |
| B10 | Numeric throughput benchmark | Missing | Medium | SB06 |
| B11 | Broad validation | Caveats remain | Medium | SB08 |
