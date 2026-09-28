# Handoff package validation

This document describes checks of the **handoff archive**, not CanDoItAll product behavior.
The final seal, test output and archive digest are produced after this file is written.
See [PROOF_STATUS.md](PROOF_STATUS.md) for product execution limitations.

## Expected structural inventory

- 22 shared v3 foundation files retained byte for byte from the preceding Plugins package.
- 14 current module files, including this document and the root manifest.
- 35 current source/metadata records, with explicit coverage and pinned repository-file URLs.
- Root SHA-256 manifest covers all package files except itself; nested shared manifest remains original.
- Only UTF-8 Markdown, JSON, Python and SHA-256 text; no fonts, binaries, repository source archive,
  raw customer data, credentials, screenshots or claimed compiled regression seeds.

## Checks to execute before delivery

1. Run `python tools/validate_package.py`: UTF-8/JSON, safe local links, required entry files,
   review SHA consistency, source IDs/paths/object IDs, manifest coverage and byte hashes.
2. Run `python tools/test_package.py`: 11 positive/adversarial disposable-package cases.
3. Run `python shared/tools/validate_bundle.py` and `python shared/tools/test_tooling.py`:
   unchanged shared integrity and 14 inherited utility cases, with Git writes only in synthetic repos.
4. Compare every shared file with the preceding package byte for byte; ensure exactly the same set.
5. Check Markdown fence balance, Python syntax, archive inventory/path safety, CRC and extraction;
   validate the extracted copy, and compute the delivered ZIP SHA-256 outside the archive.

The root utility does not resolve external URLs, check Markdown anchors, verify source authenticity,
run .NET or certify runtime behavior. The external ZIP digest is an integrity aid, not a digital
signature. Git object IDs in sources.json came from connected source responses and are not a
substitute for implementation-entry drift review.

## Delivery result

**Pass.** The root validator found 36 files, 56 valid local file links, five JSON files,
35 source/metadata records and 35 root manifest entries. The module utility tests passed
11/11 with zero skipped cases; inherited utility tests passed 14/14 with zero skipped cases.
The unchanged shared validator passed. All 22 shared files matched the prior package byte
for byte. Python AST and Markdown fence checks passed. The final ZIP CRC/inventory and its
extracted-copy manifest/link validation passed before delivery. No product runtime proof is
implied by these package checks.
