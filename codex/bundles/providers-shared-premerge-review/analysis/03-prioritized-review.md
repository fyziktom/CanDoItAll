# Prioritized Pre-Merge Review

**Repair and verify before merge. No production fixes made.**

| ID | Priority | Problem | Owner |
| --- | --- | --- | --- |
| SP-01 | P1 | Successful buffered Responses containing error:null become 502. | SB01 |
| SP-04 | P1 | Failed/incomplete/broken streams lose explicit public failure and end as clean 200 EOF. | SB01 |
| SP-02 | P2 | Default-policy loopback imports pass discovery but fail runtime selection. | SB02 |
| SP-03 | P2 | Error bodies over 512 bytes lose 429/504 classification and Retry-After. | SB01 |
| H01 | P2 | Quoted JSON credential keys evade optional Detailed capture redaction. | SB03 |
| H02 | P2 | Explicit provider timeouts become cancellation/generic failure in history. | SB03 |
| PERF-01 | P2 | Expired standalone input-detail tombstones are never deleted. | SB04 |
| PERF-03 | P2 | Redundant full-body copies/parses inflate relay allocation cost. | SB05 |
| PERF-04 | P2 | Warm route lookup still materializes/evaluates the full catalog. | SB05 |
| PERF-05 | P2 | Constant validation sets are rebuilt at 29 runtime call sites. | SB05 |
| DC01 | P2 | Documentation validator fails for six new projects. | SB07 |
| DC02 | P2 | OpenAPI request fields and custom scalar/enum semantics are missing. | SB06 |
| DC03 | P2 | SharedInfo snapshot is stale; shared-provider API skill absent. | SB08 |
| DC04 | P2 | Original docs/export/closure unfinished; new changes postdate old proof. | SB07/SB09 |

Exact SP/H02/DC locations, scenarios and fixes: [provider report](provider-review.md), [documentation report](docs-contracts-review.md).
[Performance report](performance-review.md) contains both passes, recipe checklist, exact counts and three additional P2 hot-path improvements assigned to SB05. Recipe hits are not defects or measured speedups.

H01: HistoryTextCapture.cs:9-12,34 under MAF/ProviderHistory/Application accepts a key followed by whitespace then colon/equal, but not a closing JSON quote. The source regex redacts password: fixture-secret but leaves quoted password/api_key/client_secret keys unchanged. The prior architecture/09-search-security-contract.md:127 promises these allowlisted patterns. [Synthetic reproduction](redaction-reproduction.json) used no real secret or retained data. Source SHA-256: C8949B979678AE6D9464B1F83AEE7EEE837BB45EBB3CF41330D2C699B514FF4A.

H02 fixes must recognize explicit timeout cause/deadline and caller cancellation separately; do not convert every independent OperationCanceledException into TimedOut. Preserve already observed terminal success/usage.

SP-04 qualification: repository raw streaming consumers reject missing terminal markers. The public endpoint still drops failures; official OpenAI SDK source permits ordinary EOF. Add failing-first proof with the pinned SDK.

Not promoted to bugs: silent recovery overwrite (EF concurrency tokens reject stale writes), singleton lease after live profile switch (production switching requires process restart), and invisible SDK HTTP retries (accepted application-visible attempt semantics). Native Responses output replay and inner Ollama retries remain documented compatibility/observability risks, not invented feature scope.

Targeted review cannot certify all 648 changed paths. Old test counts are not current passes.
