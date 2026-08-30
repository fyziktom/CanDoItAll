# Pattern Selection Records

| Force | Decision | Rejected alternative | Proof |
| --- | --- | --- | --- |
| Cross-cutting history capture | Keep existing typed decorators; correct outcome classification | New generic reflection/object-based logging pipeline | Direct fake-driver/client tests and actual composition capture. |
| External SSE failures differ from pre-header failures | Existing Web transport adapter emits sanitized protocol failure or aborts | Silent EOF or forwarding raw upstream error | Pinned external SDK fails; ledger retains corresponding failure. |
| Network acceptance differs across discovery/runtime | Reuse a canonical policy decision through existing boundary | Grant AllowPrivateNetwork globally to make loopback work | Positive loopback and negative DNS/private-address tests. |
| Catalog freshness currently incurs full materialization | Split cheap stamp lookup from expensive projection within existing service | Process-only cache trusting stale authorization | Cross-instance revocation/secret deletion proof and allocation/query baseline. |
| Constant role/field allowlists repeatedly allocated | Cache static immutable sets with existing comparer | New rule engine or broad request-policy extraction | Exact allowed/denied request parity and allocation reduction. |
| Repeated body copies/parses | Pass existing owned ReadOnlyMemory to supported parser overload; share parse only with clear lifetime | Unsafe spans, pooled data escaping lifetime, unbounded buffering | Payload limits, returned buffer ownership and allocation proof. |

No new project/interface is selected. Existing boundaries are real and testable; reduce repeated work without shifting security responsibility into caching or UI.
