# Package validation

These checks certify the handoff, not the CanDoItAll product.

The final package contains **38 UTF-8 text files**, including **22 shared-v3 files preserved
byte-for-byte** from the supplied Resources handoff. The root SHA-256 manifest covers its
other 37 files, including the unchanged shared manifest. There are 33 source-register entries
and five JSON files. Internal relative-file links are checked; Markdown anchors, external URLs,
source authenticity and product/runtime behavior are not certified by the package utility.

## Checks performed

- Root integrity/metadata/source-marker/local-link validation: passed.
- Root utility tests: **11 passed**, none failed or skipped.
- Unchanged shared integrity validator: passed.
- Shared utility tests: **14 passed**, none failed or skipped.
- Shared file-name set and bytes compared with the prior handoff: all 22 identical.
- Final ZIP CRC test and freshly extracted-copy validation: passed.
- Standalone English prompt compared with the ZIP entry: identical.

The 25 utility tests are **not** C# product tests. No product build, .NET test discovery,
Playwright journey, database/vault fault injection or watch measurement was executed here.
The production validation obligations remain in [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md).

Reproduce from the extracted root:

```powershell
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py --root shared
python shared/tools/test_tooling.py
```

Source review, historical implementer receipts and package proof are distinguished in
[PROOF_STATUS.md](PROOF_STATUS.md). No generated binaries, font files, credentials or product
logs are included in this archive.
