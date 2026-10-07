# Handoff validation

This file reports package integrity checks only. No product build, C# test, PostgreSQL test,
Playwright case or benchmark has been executed by the author of this review.

The validator checks declared documents, UTF-8, JSON shape, pinned-source identity structure,
relative Markdown target existence, no execution pin, desktop scope, no new paid/remote writes,
all-NOT_RUN evidence, unchanged 22-file shared foundation and exact manifest inventory/digests.
It does not contact every external URL, resolve Markdown heading anchors or certify runtime behavior.

Validation commands (use bytecode suppression so sealed directories remain unchanged):

```text
python -B tools/validate_package.py
python -B -m unittest discover -s tools -p test_package.py
python -B -m unittest discover -s shared/tools -p test_tooling.py
```

The 14 package negative controls test mutation detection, missing inputs, broken links, small
viewports, increased live budget, fabricated product success, unknown source, modified shared input,
unsafe manifest paths, preclaimed A1 completion, execution pin, remote writes and source mismatch.
The 14 inherited shared-tool tests remain unchanged. Counts describe tooling only.

## Executed checks

- The sealed package contains 44 files, 28 pinned source entries, 54 relative Markdown file links
  and 20 unexecuted product-evidence groups. Local file targets and JSON metadata validate.
- All 14 package tests and all 14 unchanged shared-tool tests passed, without skipped cases.
- All 22 shared files match the previous delivered P2 package byte-for-byte and match their SHA-256
  provenance. The shared validator passes independently.
- Root manifest inventory and every file digest are verified; ZIP CRC and a freshly extracted copy
  are checked by the finalizer. No extra bytecode, font or binary content is included.

These checks are performed on this artifact. They are not application tests or a count of runtime
coverage. The product evidence template intentionally remains NOT_RUN throughout.

