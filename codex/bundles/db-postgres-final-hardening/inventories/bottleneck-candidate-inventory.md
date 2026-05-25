# Bottleneck candidate inventory

| Area | Current status | Remaining risk |
|---|---|---|
| Runtime DbContext creation | Pooled canonical factory exists | Good; guard against profile factory leaking into hot path |
| DB switching | Restart-first activation exists | Naming still says switch; make semantics explicit |
| Automation delivery | SKIP LOCKED + grouped bounded parallelism | Need numeric proof and duplicate negative tests |
| Process outbox | SKIP LOCKED + partitioned parallelism | Finalization needs lease-token conditional update |
| Connector outbox | SKIP LOCKED + partitioned parallelism | Finalization needs lease-token conditional update |
| Process dispatch | Claim-first headers + durable claim | Need query-count proof and claim-loss negative tests |
| Profile transfer/schema | Profile-specific contexts | OK if maintenance-only boundary is enforced |
| Validation | focused tests passed | Broad test caveats remain |
