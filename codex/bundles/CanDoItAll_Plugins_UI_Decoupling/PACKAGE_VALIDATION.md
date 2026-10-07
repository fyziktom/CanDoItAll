# Package validation

This record concerns the generated handoff, not product correctness.

The unchanged shared v3 validator passed, and its 14 Python tooling tests passed with no failures or skips. The new module package is separately checked by `tools/validate_package.py` for UTF-8 inventory, JSON/metadata, source identity/coverage metadata, internal local file links and SHA-256 manifest consistency. It does not fetch remote source, validate Markdown anchors or execute C#.

All 22 included shared files are byte-identical to the supplied shared v3 archive. The historical shared source register is preserved, not rewritten to the new commit. No previous module-specific execution bundle is recursively embedded again.

The final ZIP is checked using its CRC/integrity test and entry-by-entry byte comparison against the sealed package. The standalone prompt is identical to the packaged `prompt.md`. The SHA-256 sidecar covers the ZIP, not the repository.

The reviewer did not build or execute CanDoItAll, run its PostgreSQL/browser tests, inspect ignored implementer TRX/screenshots or measure its development loop. Current TestLab receipt counts are attributed to the implementer. New Plugins requirements are an execution assignment, not claimed completed work.

Recheck this package without changing it:

```text
python tools/validate_package.py
python shared/tools/validate_bundle.py --root shared
```

## Executed checks

- Shared validator: passed; shared tooling tests: 14 passed, 0 failed.
- New package validator: passed; rejection smoke checks detected changed bytes and a broken local link.
- Included shared files: 22 of 22 byte-identical to the supplied ZIP entries.
- Final inventory: 34 text files, 33 manifest entries, 64 local file links, 5 JSON files and 29 source records.
- ZIP integrity and entry bytes, standalone prompt equality and archive SHA-256: checked after sealing.

The first local rejection-smoke attempt detected generated Python bytecode in the text-only package before reaching its intended assertion. That temporary inspection artifact was removed and bytecode generation disabled; the rejection checks and clean final package validation were then rerun successfully.
