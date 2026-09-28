# Handoff package validation

This document records checks of the handoff, not product execution. The reviewer
had no .NET SDK and did not run product tests. The source findings remain
source-derived until reproduced by the executor.

## Checks performed for the delivered archive

The package is sealed by `MANIFEST.sha256`. Its validator checks all declared
files, UTF-8 text, local Markdown destination paths, JSON parseability and metadata,
unique source IDs, pinned repository-file references, safe paths and SHA-256 hashes.
It rejects symlinks, unexpected binaries, unsealed files and an execution-pinned
review commit. Source IDs include the current Memory (`ME`) family.

The retained shared v3 foundation is compared byte-for-byte with all 22 files in
the supplied Scheduler handoff. Its own validator and 14 tooling unit tests are
run separately. The root validator has 11 unit tests, including a negative unknown
Memory source ID check. These tests exercise disposable handoff copies, not C#.

ZIP validation checks `testzip`, its safe complete entry inventory, exact content
round trip and the extracted package's validator. No fonts, product binaries,
credentials, raw test evidence, Python bytecode or external symlinks are included.

Exact observed counts are recorded below from the packaging run. The archive
checksum is supplied next to the ZIP rather than placed inside a self-referential
manifest.

## Scope limits

The validator does not certify external links, downloaded source authenticity,
Markdown anchor existence or product behavior. GitHub sources were read separately
through the connected tool, as documented in [SOURCES.md](SOURCES.md). The shared
source-drift helper describes its original historical baseline, not this new
Memory implementation inventory; do not mistake its exit status for a current
product gate. Follow current repository policy for actual execution.


## Final local result

- 36 UTF-8 text files; 5 JSON documents parsed.
- 51 local Markdown destination links checked.
- 49 source entries; 35 files sealed by the root manifest.
- Root utility tests: 11 passed, 0 failed, 0 skipped.
- Shared utility tests: 14 passed, 0 failed, 0 skipped.
- All 22 shared files equal both the extracted preceding handoff and its original ZIP bytes.
- Root/shared validators passed; final ZIP inventory, CRC and extracted-copy validation passed.

Total utility tests: 25. These are handoff-tool tests only, not application tests.
