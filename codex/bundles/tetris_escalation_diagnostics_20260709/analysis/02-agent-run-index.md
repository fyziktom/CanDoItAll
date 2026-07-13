# Agent Run Index

## Root Run Agent Attempts

| Time | Execution run | Step | Branch | Key observation |
| --- | --- | --- | --- | --- |
| 05:10:00 | 3600a9ca-5e49-479e-acc4-d91f3836b459 | feature-intake | none | Scope packet completed. |
| 05:21:57 | 93b294d8-3801-4c42-b0b1-92af76101173 | peer-review | none | Peer review completed, noted residual runtime-proof risk. |
| 05:23:08 | 53e33ea0-6d24-436e-bff9-890cb0a858a3 | qa-validation | repair-required | Restore/build/test succeeded, browser proof missing. |
| 05:23:41 | 815724a7-c93f-4c20-9a30-2ea57ae090ff | qa-validation | repair-required | Still missing validation/browser chain; outcome text declares unresolved blocker. |
| 05:24:10 | 32e89fa4-03d2-4c9d-8d21-62f16d00d30d | qa-validation | quality-accepted | Claimed runtime/browser proof and product shell. Adapter later rejected due scaffold content. |
| 05:25:11 | 5499ef7c-0a72-4b44-8355-97589d5eb06d | qa-validation | repair-required | Correctly selected repair-required for scaffold content, but lacked browser/runtime receipts and triggered final block. |

## Receipt Pattern

See `analysis/root-agent-receipt-summary.txt` for root-run receipt counts. The important contrast is:
- The `quality-accepted` QA attempt has runtime/browser receipts.
- The final `repair-required` QA attempt has no full runtime/browser proof chain.
- The adapter still enforces missing browser/runtime receipts on the final repair-required attempt.

## Child Run Agent Attempts

Use `api/child-runs-summary.json` and `api/child-runs/<run-id>/agent-execution-runs-list.json` for all child execution runs. Notable child validation:
- `4061331e-1a71-421b-807a-a01ce08c60c3`, step `validate-first-build`, branch `setup-validated`, had restore/build/test receipts.
- `fef178ba-8721-4550-aab5-f971523957cd`, step `code-change`, had 32 receipts including restore/build/test and product writes.
- `1fb9c330-85c7-419a-9a55-6decb509fe4b`, step `add-tests-and-proof`, had restore/build/test receipts.

No child run appears to remove all default scaffold files from the generated product; see product-output snapshot.
