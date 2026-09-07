# A05 cancellation lifetime adjudication

Fifteen meaningful characterization cases passed on the existing Overview lifetime implementation: six session cases (replace/dispose crossed with late success/failure/cancellation) and nine usage-dialog cases (consumer/provider/model crossed with those outcomes). The source captures each token, ignores cancellation initially, registers only after cancellation, then completes. Tests observe prompt cancellation, immediate delayed-registration callbacks without ObjectDisposedException, no late success/error/finally publication, preservation of a newer loading owner, and disposed token wait-handle resources. Idempotent production Dispose guards establish exactly-once disposal ownership.

These are negative findings: the suspected early-disposal failure did not reproduce on the recorded .NET 10 runtime. No production Overview session, usage-dialog, or page dialog-group lifetime code was changed. The tests protect the public lifetime contract and do not depend on private component fields or raw CancellationTokenSource implementation counts.

The exact discovered names and GREEN receipts are shared with [A03](../A03/green/results.json); the final owning tests reran them as part of the 80 Unit and 88 Components selections. No future-runtime or unexecuted platform guarantee is inferred from these results.
