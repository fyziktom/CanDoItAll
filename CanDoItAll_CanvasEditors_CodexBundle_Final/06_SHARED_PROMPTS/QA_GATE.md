
# QA gate

An item fails the QA gate if **any** of the following is true:

- a covered note has no traceable implementation item,
- required files in the item folder are missing,
- acceptance criteria were not evaluated,
- required tests were not run,
- a UI-changing item has no screenshots,
- screenshots exist but do not semantically prove the behavior,
- the implementation violates normalized decisions,
- the 44-node Prompt Factory bugfix has no root-cause evidence,
- a browser-impossible behavior is still assumed,
- secrets are stored in plain text where a secret reference was expected.

The final bundle is approved only when all items pass this gate.
