# Safe working evidence

Copy evidence.json and model-parity.json to an ignored task-owned directory. Do not modify the
sealed inputs in any history package. Every original PP2 group remains, plus local concurrency.
Use PASS / FAIL / BLOCKED / NOT_RUN / NOT_APPLICABLE with actual attempt IDs, source/image origins,
private raw evidence paths and safe summaries. A phase being out of time or unavailable is not N/A.
Final mandatory groups must be PASS for a full PP2-complete claim. See EXECUTION_AND_CLOSURE.

The closure checker is intentionally strict: final-source fields, all required groups/gates,
verified signed commit metadata and evidence paths must be present. It verifies consistency only,
not file provenance or actual Git/C# execution. Never invent values to satisfy it. The model checker
uses the existing safe export schema; it does not cover local alias/enabled concurrency, prices or
all runtime facts on its own. Those have their own native/browser evidence.
