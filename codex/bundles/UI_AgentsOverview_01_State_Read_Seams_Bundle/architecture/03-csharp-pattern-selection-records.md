# Pattern decisions

| Force | Choice | Rejected alternatives | Direct proof |
|---|---|---|---|
| Overlapping reads/routes | Per-page session, separate CTS/generation lanes. | Global busy flag; scoped singleton; generic controller. | Current-page RED and direct latest-wins/cancel/dispose tests. |
| Independent header failure | Separate read operations on existing query interface. | Combined Task.WhenAll coupling; interface-per-read. | Header survives aggregate failure; zero unrelated scope reads; registered adapter. |
| Header/dashboard across tabs | Derive from one accepted overview. | Copied totals; session only under conditional Overview. | Public same-generation counts and history policy. |
| Render without runtime | Controlled Surface + pure mapping; reuse Usage values. | Wrapped page with DI; fake charts. | No-feature-service render, intents, real browser. |
| Existing effect owner | Page keeps thin typed intent dispatch. | Forwarding host/controller or service bag. | Route/dialog ownership through actual page UI. |
| Mutable descendants/options | Capture independent collections and renderer-owned options from pure definitions. | Assuming record/IReadOnlyList freezes arrays; shared mutable static chart options. | Two-instance public independence test. |

No inheritance/event bus/universal receipt/effect framework. Future names may vary while the responsibility, test seam and old-owner removal remain reviewable.
