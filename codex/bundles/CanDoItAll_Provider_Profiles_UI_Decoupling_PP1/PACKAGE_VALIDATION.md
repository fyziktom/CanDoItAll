# Package validation

The final measurements are recorded after running the package and shared self-tests, root/inner manifests, local-link validation and a fresh ZIP extraction check. The package contains only UTF-8 Markdown/JSON/Python and SHA-256 manifests, not application binaries, patches, logs, fonts or credentials.

All 22 shared v3 files are copied byte-for-byte from the supplied A2 archive, with unchanged shared-provenance hashes. The original application-review SHA in shared v3 remains historical evidence, not the PP1 execution pin.

The evidence template is immutable and begins with every product group NOT_RUN. No package-author product test, aggregate completion or release flag is prefilled. Tooling tests validate this archive, not C#, native effects, external links or the application.

## Final package checks

- 49 UTF-8 files; all 48 non-root-manifest files are covered by the root SHA-256 manifest.
- 29 source-register entries with exact review refs, observed Git blob IDs and explicit read coverage.
- 60 local Markdown file links resolve within the package. External URLs and heading fragments are not runtime-tested by this validator.
- All 22 shared v3 files match the supplied A2 archive byte-for-byte and match the unchanged inner provenance/manifest.
- 14 package self-tests and 14 shared tooling tests pass: 28 auxiliary tests, zero failures.
- All 28 product outcome groups remain NOT_RUN, with no preclaimed completion or product execution.
- ZIP CRC, traversal-safe extraction, inventory equality, per-file hashes and validation of a fresh extracted copy pass.
- The generated standalone prompt and Czech review match their packaged originals.

Validation commands:

```text
python -B tools/validate_package.py
python -B -m unittest discover -s tools -p "test_*.py"
python -B shared/tools/validate_bundle.py --root shared
python -B -m unittest discover -s shared/tools -p "test_*.py"
```

Application source was read through the connected GitHub tool. No .NET SDK is available in this review environment, so no product build, C# test, PostgreSQL execution, browser run or watch measurement was performed by the package author. The recorded A2 results are the implementer's checked-in evidence, not independently rerun results.
