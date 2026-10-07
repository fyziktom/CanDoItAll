# Package validation

This package contains guidance, provenance, unexecuted evidence templates and lightweight
Python validation utilities. It contains no application patch, dependency binary, fonts,
private runtime evidence or credentials.

The supplied shared v3 directory is copied byte-for-byte from the WB4 archive. Its original
manifest and tools remain unchanged. The root manifest seals every package file except itself.
The root checker validates UTF-8/JSON, local Markdown file links (not fragment anchors or remote
connectivity), source-reference IDs, metadata, template groups, shared hashes and the manifest.
It does not authenticate native outcomes, verify external repository content, execute the
application or certify architecture/testing success.

Run from the extracted root:

```text
python tools/validate_handoff.py
python -B tools/test_handoff.py
python shared/tools/validate_bundle.py
python -B shared/tools/test_tooling.py
```

The shipping `templates/evidence.json` intentionally contains only NOT_RUN product groups.
A separate working evidence file may be structurally checked with:

```text
python tools/validate_handoff.py --evidence /path/to/owned/evidence.json
python tools/validate_handoff.py --evidence /path/to/owned/evidence.json --require-complete
```

The second command rejects NOT_RUN/FAIL/BLOCKED groups. QUALIFIED requires a written reason
and no unresolved product blocker; accepting its JSON structure still does not endorse that
reason. No working evidence is present in this package. Validators never stage, commit, fetch,
run external services, modify the input or change product test rules.
