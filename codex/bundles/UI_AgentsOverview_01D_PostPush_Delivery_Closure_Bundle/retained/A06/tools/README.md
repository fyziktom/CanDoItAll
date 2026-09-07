# Executed closure helpers

These exact helpers preserve the executed reconciliation and complete scan commands. They refer to the ignored scratch layout used by this run and write receipts there or into this follow-up. For reproduction, copy them into a fresh scratch layout and redirect output to a new receipt directory; do not rerun them over sealed historical evidence. Stable execution itself is recorded one level above in run-stable.py. The maintained reusable proposed/tree manifest and encoding validators live in tools/Validation/bundles at the repository root.

Primary and Components scans compare every proposed Git text file with that repository's own HEAD, without a size cutoff. Retained scanning also decompresses gzip and decodes historical UTF-16. Discovery reconciliation uses fresh TRX and discovery, permits only explicitly source-verified old deferred/display cases, and does not reuse old runtime results.
