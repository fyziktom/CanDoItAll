# Handoff validation

This package is an instruction/evidence bundle, not a source patch or a test run of CanDoItAll.
The optional integrity utility checks metadata, declared files, local Markdown targets, source
reference identity, UTF-8 text, shared-v3 hashes, initial evidence and the SHA-256 manifest.
It does not check remote URL availability, execute C#, inspect generated UI or certify runtime behavior.

Shared foundation: exactly 22 original files copied byte-for-byte from the supplied A1 bundle,
including its historical audit and own manifest. `shared-provenance.json` records their original
identities. No old historical handoff is recursively included beyond this common foundation.

Product evidence begins with every group NOT_RUN, no evidence entries and `agent_editor_a2_complete`
false. Copy that template outside the sealed package when executing the task; do not edit it in place
to manufacture a green package validation result. Final product acceptance also needs the semantic
checks in CLOSURE.md and actual repository gates.

Reproduce package checks without adding Python cache files:

```text
python -B tools/validate_package.py
python -B tools/test_package.py
python -B shared/tools/test_tooling.py
```

The author's delivery process additionally checks ZIP CRC, fresh extraction, preserved shared bytes
and a SHA-256 of the ZIP. Those results apply to this handoff only. Source repository branch reads,
review coverage and no-runtime-execution status are recorded separately in review-provenance.json.

## Author-observed package results

The final delivery contains 50 text files, 28 product proof groups, 28 source records and
56 local Markdown links. The package and shared tools passed 14 tests each (28 total).
Every supplied product group remains NOT_RUN. ZIP CRC and validation of a fresh extraction
passed; all 22 shared-v3 files match the supplied A1 archive byte-for-byte.
No product build, C# test, browser or dotnet-watch execution was performed by the reviewer.
