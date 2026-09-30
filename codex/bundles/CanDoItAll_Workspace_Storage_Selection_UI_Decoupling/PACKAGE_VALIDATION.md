# Package validation

The final handoff passed structural and integrity checks. This is package proof, not product proof.

| Check | Result |
|---|---|
| UTF-8 text inventory | 40 files; 5 JSON documents parse |
| Local Markdown link destinations | 57 valid destinations within the package |
| Source register | 41 unique source records with explicit coverage |
| Root SHA-256 manifest | 39 entries, complete inventory excluding its own manifest |
| Shared v3 foundation | 22 files byte-for-byte equal to the provided previous bundle |
| Root validator regression tests | 11 passed; zero failed/skipped |
| Shared utility tests | 14 passed; zero failed/skipped |
| Archive and extraction | ZIP CRC check and fresh-extraction root/shared validation passed |

Commands:

```text
python -B tools/validate_package.py
python -B tools/test_package.py
python -B shared/tools/validate_bundle.py
python -B shared/tools/test_tooling.py
```

The root validator verifies UTF-8 inventory, metadata, source-ID consistency, local link destinations and a complete SHA-256 manifest. It does not certify Markdown anchors, external URLs, GitHub source authenticity or application behavior. The shared validator verifies its sealed v3 reference independently. External source authenticity comes from the connected review, within each record's stated coverage, not from this utility.

The shared reference is copied unchanged; previous module execution documents are not recursively included. Tests use disposable package copies and synthetic repositories, never the user's checkout. The ZIP is accompanied by an external SHA-256 file.

No .NET build, product test, browser or benchmark was executed by this package validation.
