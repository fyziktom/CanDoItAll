The first all-suite execution is retained as a failed broad gate. It completed with9,747 case results:9,731 passed,15 failed and1 opt-in case skipped.9,746 cases executed. All39 additional runtime cases are explained by deferred theories(34 Unit and5 Integration); discovery did not omit a method.

| Suite | Discovered | Runtime cases | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|---:|
| Unit | 7,187 | 7,221 | 7,219 | 2 | 0 |
| Components | 1,191 | 1,191 | 1,189 | 2 | 0 |
| Integration | 1,330 | 1,335 | 1,323 | 11 | 1 |

The two Unit failures are existing HEAD repository guard mismatches: the Docker COPY assertion predates the existing source-context exclusions, and the naming guard detects a pre-existing SB09 literal in a Playwright test. Neither guard nor the source it checks was changed to make the gate pass.

The two Components workflow-preview cases passed2/2 on their one authorized quiet focused rerun, using the same frozen binaries and original30-second waits. Their first broad failures remain recorded. The result supports non-reproduction under the focused condition; it does not prove the earlier cause. In particular, application builds had already finished before the original failures. The proposed initialization-race explanation remains an inference. Root authorized an older-output comparison only if either candidate case failed again; because both passed, no older test output was executed.

Ten Integration failures occurred during the owned test PostgreSQL1GiB tmpfs exhaustion, beginning with a write-ahead-log PANIC. After replacing only that disposable server with the same image/credentials/endpoint/CPU settings and larger approved capacity, the entire affected ProviderHistoryQuery and ProviderHistoryCapture classes passed23/23, including the million-entry case. The remaining strict index-name guard repeated its failure on the healthy replacement: the planner chose an efficient primary-key bitmap index scan(0.119ms, initially0.108ms) rather than the literal index name required by the assertion. No planner, SQL, index or test source was changed. The live Ollama case was correctly skipped because its opt-in is disabled.

The unchanged original2-second checkpoint-deadline test passed in the first full Integration execution. Both combined startup/provider-boundary failure-persistence cases also passed there. Their isolated runtime fixture proves production terminalization, durable failure logs, caller/activity propagation and reopening, not a real HTTP adapter failure; live MCP/provider success proof belongs to the separate UI validation.

All authorized follow-ups are complete: History classes23/23 passed; strict index-name case0/1 passed; Components pair2/2 passed. No broad suite was repeated. The13 frozen source hashes and15 recorded binary hashes matched before the follow-ups. No application host was changed by this validation work. Root must explicitly adjudicate the retained unrelated/non-reproduced failures for the startup-specific gate; this report does not claim that all suites are green.

Exact counts, commands, cases and results are in `final-broad-and-attribution-summary.json`, the first suite summaries, and the focused phase summaries. Full original TRX/transcripts remain in the owned artifact directory. The original2-second deadline and both failure cases are also preserved individually in the sanitized proof.