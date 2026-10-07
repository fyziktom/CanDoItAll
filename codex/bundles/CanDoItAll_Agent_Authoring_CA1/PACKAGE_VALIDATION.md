# Package validation (not product validation)

The package validator checks the exact manifest, local document links, unique source/group IDs,
unchanged 22-file shared foundation and an unexecuted product-evidence template. Its unit tests
exercise incomplete/forged counters, missing provenance/signatures, path traversal and changed files.
The final delivery validation report outside the ZIP records actual checks, archive CRC and
byte-for-byte equality after extraction. No .NET/Docker/browser/watch product execution is claimed.

```text
python tools/validate_handoff.py
python tools/test_handoff.py
python shared/tools/test_tooling.py
python shared/tools/validate_bundle.py
```

Actual implementation evidence is kept outside the package. At closure:

```text
python tools/validate_handoff.py --evidence /path/to/actual/evidence.json --require-complete
```

A structural PASS cannot certify authenticity, sufficiency of a browser assertion, native effects
or product correctness. Retain human review of the evidence and the semantic validation matrix.
