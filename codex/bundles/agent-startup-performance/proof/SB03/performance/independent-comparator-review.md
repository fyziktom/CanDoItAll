# Independent candidate timing comparator review

Status: PASS for the prepared comparison method. The comparator has not been executed against candidate samples; this is not a performance acceptance result.

The current comparator enforces five before and five after warm samples per host, each with five distinct sessions. The first after-start sample must precede the warm samples and have its own session. The continuation is separate and must reuse one earlier measured warm session. First-before-start evidence remains explicitly unavailable; no cold-start comparison is invented.

Candidate dispatch metadata must identify the exact HTTP request-start diagnostic boundary, the direct HTTP-parent-to-run activity association, and nonempty trace/span/parent IDs. Unmatched or other-agent requests cause refusal. Each UI send/completion interval must select exactly one run, each run must be used once, and every observed run must have a UI sample. Browser origin, finite ordered monotonic markers, ordered UTC markers, nonnegative server duration, completed state and dispatch-before-completion are checked.

The HTTP span extractor remains byte-identical to the baseline copy: SHA256 6C9882AE4928887226178D8899CDAA14785B34AFBCFC3F16511751C6D656C141. It joins the HTTP parent span to the run activity span with the same trace ID and requires a unique run. Timestamp proximity or a trace-only match cannot replace this association.

Before and after client clock evidence must each contain at least three brackets. Every offset and uncertainty must be finite and uncertainty nonnegative. The tightest measured bracket is used, and the candidate absolute offset plus uncertainty must fit inside the one-second UI association bracket. Same-server Created-to-dispatch intervals are the primary metric. Submit-to-dispatch is also reported raw and offset-aligned, with combined before/after uncertainty; a delta inside the bound remains indeterminate. Native submit/server markers are explicitly assumed to share the same Windows clock, not presented as an independently measured zero-drift guarantee.

The warm median threshold remains at least 15% and 0.5 seconds. The predefined maximum-regression trigger requests another matched batch. Continuation and first-start results stay separate. The comparator's numerical result alone cannot establish source/configuration equivalence, correct tools/approvals/results, or UI acceptance. These require the separate frozen-host and Playwright MCP evidence.

Review validation: read-only source inspection, exact extractor hash comparison and Python AST syntax parse, exit 0. No candidate calculation, app call, build or test was started during this review. Frozen script hashes and review metadata are in independent-comparator-review.json.