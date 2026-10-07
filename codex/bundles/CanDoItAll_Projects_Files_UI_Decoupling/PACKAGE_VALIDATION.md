# Package validation

This report covers the handoff archive only, not C# builds, database integration, browser runs or speed measurements. The source review used authenticated GitHub reads. No .NET SDK was available in the review environment; private runtime evidence was not supplied.

The package validator checks declared documents, UTF-8/JSON, pinned-source metadata, relative links, unchanged shared-v3 SHA-256 identities, supported-desktop scope, no paid/remote-write authorization, NOT_RUN evidence defaults and the complete manifest. External URLs and product results are not runtime-validated by this script.

Run from the extracted package:

```bash
PYTHONDONTWRITEBYTECODE=1 python tools/validate_package.py
PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s tools -p 'test_*.py' -v
PYTHONDONTWRITEBYTECODE=1 python -m unittest discover -s shared/tools -p 'test_*.py' -v
```

## Executed package checks

- 44 UTF-8 text/code/metadata files in the archive, including its manifests.
- 28 reviewed source records and 60 local Markdown links validated.
- All 22 shared-v3 files preserved byte-for-byte against the supplied P1 archive and original shared provenance hashes.
- 14 package tests passed, including negative controls for changed/missing files, broken links, unauthorized scope, source mismatch and preclaimed product success.
- 14 shared-tool tests passed. Total: 28 Python tooling tests; no C# or application tests were executed by the package author.
- Root SHA-256 manifest, ZIP CRC integrity and a freshly extracted archive copy validated successfully.
- All 18 product evidence groups remain NOT_RUN with no attached execution evidence; P2 completion is not preclaimed.

The mirrored prompt and Czech review are byte-identical to their archive entries. No application source, private runtime evidence, dependency binaries or font files are embedded outside the unchanged historical shared foundation's text files.
