# Risk register

| Risk | Probability | Impact | Control |
|---|---:|---:|---|
| Orphan or divergent conversation rows | High | Critical | SB01 single command transaction + failure injection |
| Duplicate dispatch after uncertain crash | Medium | Critical | Durable execution lease and conservative reducer |
| Cancellation races semantic success | High | High | Monotonic cancellation evidence in finalization CAS |
| Cross-profile data/provider mixture | Medium | Critical | Whole-use-case profile scope and switch cancellation |
| One instance recovers another live operation | Medium | Critical | Lease owner, heartbeat, expiry, claim fencing |
| Client disconnect cancels paid inference | High | High | 202 admission, background dispatcher, explicit cancel |
| SSE reconnect loses or duplicates text | Medium | High | Durable sequence, Last-Event-ID, gap contract, terminal snapshot |
| Retry duplicates visible partial output | Medium | High | Retry only before first emitted delta |
| Event journal grows without bound | High | High | Coalescing, byte/count limits, retention and cleanup |
| Long transcripts become O(n) per request | High | High | Bounded SQL context window and keyset paging |
| Remote client spoofs origin or overreaches scopes | Medium | High | Server-owned origin and read/manage/execute policies |
| Broad tests consume execution context repeatedly | High | Medium | Machine-enforced test budget; one final gate |
| UI begins over unstable contracts | Medium | High | FINAL checkpoint explicitly locks UI bundles |
