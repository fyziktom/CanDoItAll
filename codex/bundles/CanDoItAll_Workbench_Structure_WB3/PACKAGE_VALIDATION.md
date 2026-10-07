# Handoff validation

This record concerns the handoff archive and its helper tools, not the application.
No C# compilation, native mutation, database, browser or provider test was run by
this review. All 32 product-evidence groups remain `NOT_RUN`.

The final package contains 52 files, 51 root-manifest entries, 25 exact-ref source
records and 58 checked local Markdown links. The 22 shared v3 files match the
previous attached WB2 archive byte-for-byte. The ZIP passes CRC and the freshly
extracted copy passes the same manifest, link, source and shared-content validator.

All 30 handoff helper tests and 14 retained shared-tool tests pass (44 total).
The strict completion mode rejects the unexecuted evidence template as expected.
Passing synthetic validator fixtures are tests of that validator only; they are
never written into the shipped product-evidence template.

Reproduce the structural checks from this package root:

```text
python tools/validate_handoff.py
python -m unittest discover -s tools -p "test_*.py" -v
python -m unittest discover -s shared/tools -p "test_*.py" -v
```

The following must fail for the sealed unexecuted template:

```text
python tools/validate_handoff.py --evidence templates/evidence.json --require-complete
```

A validator cannot authenticate referenced runtime artifacts or certify source
correctness. Actual Codex execution must supply its own evidence outside this
sealed handoff and preserve any failed, blocked or qualified results truthfully.
