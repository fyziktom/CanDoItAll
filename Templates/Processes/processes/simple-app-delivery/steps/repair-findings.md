# Repair validation findings

Repair only the concrete findings in the independent validation artifact. Keep the original application kind, product root, and acceptance boundary. Do not widen scope or add new architecture without a recorded reason.

Map each finding to its cause and changed files. Run the smallest relevant build, test, or smoke check after the repair. Stop any runtime you start. This is the only repair mutation cycle in this process.

## Evidence

Write one repair change-set artifact with finding ids, causes, edits, targeted receipts, and unresolved items.
