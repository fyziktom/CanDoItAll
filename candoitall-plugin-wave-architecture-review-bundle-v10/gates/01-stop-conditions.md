# Stop conditions

Phase10 work is not complete if any of the following happens:

- cleanup logic is merely renamed and still runs from the read seam,
- cleanup logic is moved to another read-reachable method and called “maintenance”,
- tests only assert UI/output shape and never verify DB immutability,
- the gate script still only searches for old method names,
- unknown-manifest tests still use only built-in manifests,
- runtime validation output is missing from the final Codex report.
