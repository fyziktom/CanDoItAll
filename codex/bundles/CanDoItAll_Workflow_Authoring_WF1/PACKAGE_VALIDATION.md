# Package validation

Locally verified on 2026-10-04. This concerns the handoff, not the product.

| Check | Result |
|---|---|
| Package file set | 53 text files; no prior module archive recursively embedded |
| SHA-256 manifest | 52 entries plus the root manifest, exact file coverage |
| Local Markdown links | 53 resolved paths |
| Reviewed primary-source records | 29, with exact main ref, Git blob and inspected ranges |
| Shared v3 | All 22 files identical to the supplied CA1 package |
| Handoff Python self-tests | 22 passed |
| Sealed shared Python self-tests | 14 passed |
| Product groups | 32, all NOT_RUN, no invented attempts |
| Completion negative check | Unexecuted template rejected |
| ZIP | CRC, exact entry set and byte comparison against fresh extraction passed |

The 36 Python tests check integrity, provenance, local links and evidence-structure guards only.
They do not run C#, PostgreSQL, Docker, Playwright, native authority, a canvas or dotnet watch.
The review environment had no dotnet or Docker executable and no private original product artifacts.

The source refs were checked again before sealing and remained unchanged. Product findings from source
are explicitly labeled as requiring failing-first native/component reproduction. Recorded CA1 counts
remain implementer evidence, including original mixed attempts and separate follow-ups.

Package commands:

```text
python tools/validate_handoff.py
python -m unittest discover -s tools -p "test_*.py"
python -m unittest discover -s shared/tools -p "test_*.py"
```

Actual execution evidence belongs outside this immutable package. The validator cannot authenticate
that claimed product artifacts were produced by the stated command.
